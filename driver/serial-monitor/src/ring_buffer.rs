//! Lock-free ring buffer for captured serial port data.
//!
//! Designed for single-producer (kernel IRP completion routine) and
//! single-consumer (user-mode IOCTL reader) usage pattern.
//!
//! The buffer stores variable-length entries, each consisting of a
//! `CapturedDataHeader` followed by the raw data payload.
//!
//! # Performance
//! - Uses a contiguous byte array to avoid per-entry allocation.
//! - Write/read positions wrap around using modular arithmetic.
//! - Oldest entries are silently overwritten when the buffer is full.

// In kernel (no_std) mode, Vec comes from alloc instead of std.
#[cfg(feature = "kernel")]
use alloc::{vec, vec::Vec};

use crate::shared::{CapturedDataHeader, DataDirection};

/// Default ring buffer capacity: 64 KB.
pub const DEFAULT_BUFFER_CAPACITY: usize = 64 * 1024;

/// Ring buffer for storing captured serial data entries.
///
/// Each entry is a `CapturedDataHeader` + variable-length payload.
/// The buffer operates in a circular manner, overwriting oldest data
/// when full.
pub struct RingBuffer {
    /// Backing storage.
    data: Vec<u8>,
    /// Total capacity in bytes.
    capacity: usize,
    /// Write position (next byte to write at).
    write_pos: usize,
    /// Read position (next byte to read from).
    read_pos: usize,
    /// Number of bytes currently used.
    used: usize,
    /// Number of entries currently stored.
    entry_count: usize,
    /// Total entries written (including overwritten).
    total_entries_written: u64,
    /// Total entries dropped due to overflow.
    total_entries_dropped: u64,
}

impl RingBuffer {
    /// Creates a new `RingBuffer` with the specified capacity in bytes.
    ///
    /// # Panics
    /// Panics if `capacity` is less than `CapturedDataHeader::SIZE * 2`
    /// (minimum to hold at least one entry).
    pub fn new(capacity: usize) -> Self {
        let min_capacity = CapturedDataHeader::SIZE * 2;
        assert!(
            capacity >= min_capacity,
            "Ring buffer capacity must be at least {} bytes",
            min_capacity
        );
        Self {
            data: vec![0u8; capacity],
            capacity,
            write_pos: 0,
            read_pos: 0,
            used: 0,
            entry_count: 0,
            total_entries_written: 0,
            total_entries_dropped: 0,
        }
    }

    /// Creates a new `RingBuffer` with the default capacity.
    pub fn with_default_capacity() -> Self {
        Self::new(DEFAULT_BUFFER_CAPACITY)
    }

    /// Returns the total capacity of the buffer in bytes.
    pub fn capacity(&self) -> usize {
        self.capacity
    }

    /// Returns the number of bytes currently used.
    pub fn used(&self) -> usize {
        self.used
    }

    /// Returns the number of bytes available for writing.
    pub fn available(&self) -> usize {
        self.capacity - self.used
    }

    /// Returns `true` if the buffer contains no entries.
    pub fn is_empty(&self) -> bool {
        self.entry_count == 0
    }

    /// Returns the number of entries currently in the buffer.
    pub fn entry_count(&self) -> usize {
        self.entry_count
    }

    /// Returns the total number of entries ever written.
    pub fn total_entries_written(&self) -> u64 {
        self.total_entries_written
    }

    /// Returns the total number of entries dropped due to overflow.
    pub fn total_entries_dropped(&self) -> u64 {
        self.total_entries_dropped
    }

    /// Writes bytes to the buffer at the write position, wrapping around.
    fn write_bytes(&mut self, bytes: &[u8]) {
        let len = bytes.len();
        if len == 0 {
            return;
        }

        let first_chunk = (self.capacity - self.write_pos).min(len);
        self.data[self.write_pos..self.write_pos + first_chunk]
            .copy_from_slice(&bytes[..first_chunk]);

        if first_chunk < len {
            // Wrap around
            let remaining = len - first_chunk;
            self.data[..remaining].copy_from_slice(&bytes[first_chunk..]);
        }

        self.write_pos = (self.write_pos + len) % self.capacity;
    }

    /// Reads bytes from a specific position without advancing read_pos.
    fn peek_bytes_at(&self, pos: usize, dest: &mut [u8]) {
        let len = dest.len();
        if len == 0 {
            return;
        }

        let first_chunk = (self.capacity - pos).min(len);
        dest[..first_chunk].copy_from_slice(&self.data[pos..pos + first_chunk]);

        if first_chunk < len {
            let remaining = len - first_chunk;
            dest[first_chunk..].copy_from_slice(&self.data[..remaining]);
        }
    }

    /// Reads bytes from the read position and advances it.
    fn read_bytes(&mut self, dest: &mut [u8]) {
        self.peek_bytes_at(self.read_pos, dest);
        let len = dest.len();
        self.read_pos = (self.read_pos + len) % self.capacity;
    }

    /// Discards the oldest entry from the read side to make room.
    ///
    /// Returns the size of the discarded entry, or 0 if buffer is empty.
    fn discard_oldest_entry(&mut self) -> usize {
        if self.entry_count == 0 {
            return 0;
        }

        // Peek at the header to find out how big the oldest entry is
        let mut header_bytes = [0u8; CapturedDataHeader::SIZE];
        self.peek_bytes_at(self.read_pos, &mut header_bytes);

        if let Some(header) = CapturedDataHeader::from_bytes(&header_bytes) {
            let entry_size = header.total_entry_size();
            // Advance read position past this entry
            self.read_pos = (self.read_pos + entry_size) % self.capacity;
            self.used -= entry_size;
            self.entry_count -= 1;
            self.total_entries_dropped += 1;
            entry_size
        } else {
            // Corrupted header — reset the buffer to recover
            self.reset();
            0
        }
    }

    /// Pushes a captured data entry into the ring buffer.
    ///
    /// If the buffer doesn't have enough space, oldest entries are
    /// discarded until there is room.
    ///
    /// # Parameters
    /// - `timestamp`: Capture timestamp (100ns intervals since boot).
    /// - `direction`: TX or RX.
    /// - `payload`: Raw data bytes (must not exceed `MAX_DATA_SIZE`).
    ///
    /// # Returns
    /// `true` if the entry was written, `false` if the payload is too large
    /// to ever fit in the buffer.
    pub fn push(&mut self, timestamp: u64, direction: DataDirection, payload: &[u8]) -> bool {
        let header = CapturedDataHeader::new(timestamp, direction, payload.len() as u32);
        let entry_size = header.total_entry_size();

        // Entry can never fit if it exceeds buffer capacity
        if entry_size > self.capacity {
            return false;
        }

        // Discard oldest entries until we have enough room
        while self.available() < entry_size {
            if self.discard_oldest_entry() == 0 {
                // Buffer was reset due to corruption; now it's empty
                break;
            }
        }

        // Write header
        let header_bytes = header.to_bytes();
        self.write_bytes(&header_bytes);

        // Write payload
        self.write_bytes(payload);

        self.used += entry_size;
        self.entry_count += 1;
        self.total_entries_written += 1;

        true
    }

    /// Pops the oldest captured data entry from the ring buffer.
    ///
    /// Returns `None` if the buffer is empty.
    ///
    /// # Returns
    /// A tuple of `(CapturedDataHeader, Vec<u8>)` containing the header
    /// and the raw data payload.
    pub fn pop(&mut self) -> Option<(CapturedDataHeader, Vec<u8>)> {
        if self.entry_count == 0 {
            return None;
        }

        // Read header
        let mut header_bytes = [0u8; CapturedDataHeader::SIZE];
        self.read_bytes(&mut header_bytes);

        let header = CapturedDataHeader::from_bytes(&header_bytes)?;
        let data_len = header.data_length as usize;

        // Validate data_length doesn't exceed remaining used bytes
        let remaining_after_header = self.used - CapturedDataHeader::SIZE;
        if data_len > remaining_after_header {
            // Corrupted entry — reset buffer
            self.reset();
            return None;
        }

        // Read payload
        let mut payload = vec![0u8; data_len];
        self.read_bytes(&mut payload);

        let entry_size = header.total_entry_size();
        self.used -= entry_size;
        self.entry_count -= 1;

        Some((header, payload))
    }

    /// Peeks at the oldest entry without removing it.
    ///
    /// Returns `None` if the buffer is empty.
    pub fn peek(&self) -> Option<(CapturedDataHeader, Vec<u8>)> {
        if self.entry_count == 0 {
            return None;
        }

        // Peek header
        let mut header_bytes = [0u8; CapturedDataHeader::SIZE];
        self.peek_bytes_at(self.read_pos, &mut header_bytes);

        let header = CapturedDataHeader::from_bytes(&header_bytes)?;
        let data_len = header.data_length as usize;

        // Peek payload
        let payload_pos = (self.read_pos + CapturedDataHeader::SIZE) % self.capacity;
        let mut payload = vec![0u8; data_len];
        self.peek_bytes_at(payload_pos, &mut payload);

        Some((header, payload))
    }

    /// Resets the buffer, discarding all data.
    pub fn reset(&mut self) {
        self.write_pos = 0;
        self.read_pos = 0;
        self.used = 0;
        self.entry_count = 0;
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    /// Helper to create a buffer with a small capacity for testing.
    fn small_buffer(capacity: usize) -> RingBuffer {
        RingBuffer::new(capacity)
    }

    #[test]
    fn test_new_buffer() {
        let buf = RingBuffer::new(1024);
        assert_eq!(buf.capacity(), 1024);
        assert_eq!(buf.used(), 0);
        assert_eq!(buf.available(), 1024);
        assert!(buf.is_empty());
        assert_eq!(buf.entry_count(), 0);
    }

    #[test]
    fn test_default_capacity() {
        let buf = RingBuffer::with_default_capacity();
        assert_eq!(buf.capacity(), DEFAULT_BUFFER_CAPACITY);
    }

    #[test]
    #[should_panic(expected = "Ring buffer capacity must be at least")]
    fn test_too_small_capacity() {
        RingBuffer::new(1); // Way too small
    }

    #[test]
    fn test_push_and_pop_single_entry() {
        let mut buf = small_buffer(256);
        let data = b"Hello, Serial!";

        assert!(buf.push(1000, DataDirection::Tx, data));
        assert_eq!(buf.entry_count(), 1);
        assert!(!buf.is_empty());

        let (header, payload) = buf.pop().unwrap();
        assert_eq!(header.timestamp, 1000);
        assert_eq!(header.data_direction(), Some(DataDirection::Tx));
        assert_eq!(payload, data.to_vec());

        assert!(buf.is_empty());
        assert_eq!(buf.entry_count(), 0);
    }

    #[test]
    fn test_push_and_pop_multiple_entries() {
        let mut buf = small_buffer(512);

        buf.push(100, DataDirection::Tx, b"TX data 1");
        buf.push(200, DataDirection::Rx, b"RX data 1");
        buf.push(300, DataDirection::Tx, b"TX data 2");

        assert_eq!(buf.entry_count(), 3);

        // FIFO order
        let (h1, d1) = buf.pop().unwrap();
        assert_eq!(h1.timestamp, 100);
        assert_eq!(d1, b"TX data 1");

        let (h2, d2) = buf.pop().unwrap();
        assert_eq!(h2.timestamp, 200);
        assert_eq!(d2, b"RX data 1");

        let (h3, d3) = buf.pop().unwrap();
        assert_eq!(h3.timestamp, 300);
        assert_eq!(d3, b"TX data 2");

        assert!(buf.pop().is_none());
    }

    #[test]
    fn test_pop_empty_buffer() {
        let mut buf = small_buffer(256);
        assert!(buf.pop().is_none());
    }

    #[test]
    fn test_peek_does_not_remove() {
        let mut buf = small_buffer(256);
        buf.push(42, DataDirection::Rx, b"peek test");

        let (h1, d1) = buf.peek().unwrap();
        assert_eq!(h1.timestamp, 42);
        assert_eq!(d1, b"peek test");
        assert_eq!(buf.entry_count(), 1); // Still there

        // Pop should return the same entry
        let (h2, d2) = buf.pop().unwrap();
        assert_eq!(h2.timestamp, 42);
        assert_eq!(d2, b"peek test");
        assert!(buf.is_empty());
    }

    #[test]
    fn test_peek_empty_buffer() {
        let buf = small_buffer(256);
        assert!(buf.peek().is_none());
    }

    #[test]
    fn test_push_empty_payload() {
        let mut buf = small_buffer(256);
        assert!(buf.push(1, DataDirection::Tx, &[]));

        let (header, payload) = buf.pop().unwrap();
        assert_eq!(header.data_length, 0);
        assert!(payload.is_empty());
    }

    #[test]
    fn test_overflow_discards_oldest() {
        // Create a small buffer that can hold only a few entries
        // Header = 16 bytes, + small payload
        let header_size = CapturedDataHeader::SIZE; // 16
        let payload_size = 4;
        let entry_size = header_size + payload_size; // 20
                                                     // Buffer can hold exactly 3 entries
        let capacity = entry_size * 3;
        let mut buf = small_buffer(capacity);

        // Push 3 entries - fills the buffer
        buf.push(1, DataDirection::Tx, &[0xAA; 4]);
        buf.push(2, DataDirection::Tx, &[0xBB; 4]);
        buf.push(3, DataDirection::Tx, &[0xCC; 4]);
        assert_eq!(buf.entry_count(), 3);
        assert_eq!(buf.total_entries_dropped(), 0);

        // Push 4th entry - should discard entry 1
        buf.push(4, DataDirection::Tx, &[0xDD; 4]);
        assert_eq!(buf.entry_count(), 3);
        assert_eq!(buf.total_entries_dropped(), 1);

        // First pop should be entry 2 (entry 1 was dropped)
        let (h, _) = buf.pop().unwrap();
        assert_eq!(h.timestamp, 2);
    }

    #[test]
    fn test_overflow_multiple_discards() {
        // Buffer = 2 entries capacity
        let entry_size = CapturedDataHeader::SIZE + 4; // 20
        let capacity = entry_size * 2;
        let mut buf = small_buffer(capacity);

        buf.push(1, DataDirection::Tx, &[0xAA; 4]);
        buf.push(2, DataDirection::Tx, &[0xBB; 4]);

        // Push a larger entry that requires discarding both existing entries
        let large_payload = vec![0xCC; entry_size * 2 - CapturedDataHeader::SIZE];
        buf.push(3, DataDirection::Tx, &large_payload);

        assert_eq!(buf.entry_count(), 1);
        assert_eq!(buf.total_entries_dropped(), 2);

        let (h, d) = buf.pop().unwrap();
        assert_eq!(h.timestamp, 3);
        assert_eq!(d, large_payload);
    }

    #[test]
    fn test_entry_too_large_for_buffer() {
        let capacity = 64;
        let mut buf = small_buffer(capacity);

        // Try to push an entry larger than the entire buffer
        let huge_payload = vec![0xFF; capacity + 1];
        assert!(!buf.push(1, DataDirection::Tx, &huge_payload));
        assert!(buf.is_empty());
    }

    #[test]
    fn test_wrap_around() {
        // This test exercises the wrap-around logic
        let entry_size = CapturedDataHeader::SIZE + 8; // 24
        let capacity = entry_size * 4; // 96 bytes
        let mut buf = small_buffer(capacity);

        // Fill buffer with 4 entries
        for i in 0..4 {
            buf.push(i, DataDirection::Tx, &[i as u8; 8]);
        }
        assert_eq!(buf.entry_count(), 4);

        // Pop 2 entries (freeing space at the beginning)
        buf.pop();
        buf.pop();
        assert_eq!(buf.entry_count(), 2);

        // Push 2 more entries (should wrap around the end)
        buf.push(10, DataDirection::Rx, &[0xAA; 8]);
        buf.push(11, DataDirection::Rx, &[0xBB; 8]);
        assert_eq!(buf.entry_count(), 4);

        // Verify all 4 entries are correct
        let (h, d) = buf.pop().unwrap();
        assert_eq!(h.timestamp, 2);
        assert_eq!(d, vec![2u8; 8]);

        let (h, d) = buf.pop().unwrap();
        assert_eq!(h.timestamp, 3);
        assert_eq!(d, vec![3u8; 8]);

        let (h, d) = buf.pop().unwrap();
        assert_eq!(h.timestamp, 10);
        assert_eq!(d, vec![0xAA; 8]);

        let (h, d) = buf.pop().unwrap();
        assert_eq!(h.timestamp, 11);
        assert_eq!(d, vec![0xBB; 8]);

        assert!(buf.is_empty());
    }

    #[test]
    fn test_reset() {
        let mut buf = small_buffer(256);
        buf.push(1, DataDirection::Tx, b"data");
        buf.push(2, DataDirection::Rx, b"more data");

        buf.reset();
        assert!(buf.is_empty());
        assert_eq!(buf.entry_count(), 0);
        assert_eq!(buf.used(), 0);
        // Total counters are preserved
        assert_eq!(buf.total_entries_written(), 2);
    }

    #[test]
    fn test_used_and_available_tracking() {
        let mut buf = small_buffer(256);
        let data = b"test";
        let entry_size = CapturedDataHeader::SIZE + data.len();

        buf.push(1, DataDirection::Tx, data);
        assert_eq!(buf.used(), entry_size);
        assert_eq!(buf.available(), 256 - entry_size);

        buf.pop();
        assert_eq!(buf.used(), 0);
        assert_eq!(buf.available(), 256);
    }

    #[test]
    fn test_total_entries_written_counter() {
        let mut buf = small_buffer(256);

        for i in 0..10 {
            buf.push(i, DataDirection::Tx, b"x");
        }
        assert_eq!(buf.total_entries_written(), 10);

        // Drain all
        while buf.pop().is_some() {}

        // Counter persists
        assert_eq!(buf.total_entries_written(), 10);
    }

    #[test]
    fn test_mixed_directions() {
        let mut buf = small_buffer(512);

        buf.push(1, DataDirection::Tx, b"send");
        buf.push(2, DataDirection::Rx, b"recv");

        let (h1, _) = buf.pop().unwrap();
        assert_eq!(h1.data_direction(), Some(DataDirection::Tx));

        let (h2, _) = buf.pop().unwrap();
        assert_eq!(h2.data_direction(), Some(DataDirection::Rx));
    }

    #[test]
    fn test_large_payload() {
        let mut buf = RingBuffer::with_default_capacity();
        let payload = vec![0xAB; 4000];

        assert!(buf.push(999, DataDirection::Rx, &payload));

        let (header, data) = buf.pop().unwrap();
        assert_eq!(header.timestamp, 999);
        assert_eq!(header.data_length, 4000);
        assert_eq!(data, payload);
    }

    #[test]
    fn test_stress_push_pop_cycle() {
        let mut buf = small_buffer(512);

        // Push/pop many entries to stress wrap-around
        for i in 0..1000u64 {
            let payload = format!("entry_{}", i);
            buf.push(i, DataDirection::Tx, payload.as_bytes());

            if i % 2 == 0 {
                // Pop every other entry
                buf.pop();
            }
        }

        // Drain remaining
        let mut count = 0;
        while buf.pop().is_some() {
            count += 1;
        }
        assert!(count > 0);
        assert!(buf.is_empty());
    }

    #[test]
    fn test_exact_capacity_fill() {
        // Entry exactly fills the buffer
        let header_size = CapturedDataHeader::SIZE;
        let payload_size = 48; // header(16) + payload(48) = 64
        let capacity = header_size + payload_size;
        let mut buf = small_buffer(capacity);

        let payload = vec![0xFF; payload_size];
        assert!(buf.push(1, DataDirection::Tx, &payload));
        assert_eq!(buf.used(), capacity);
        assert_eq!(buf.available(), 0);

        let (_, data) = buf.pop().unwrap();
        assert_eq!(data, payload);
    }

    #[test]
    fn test_consecutive_overflow_recovery() {
        let entry_size = CapturedDataHeader::SIZE + 4;
        let capacity = entry_size * 2;
        let mut buf = small_buffer(capacity);

        // Push many entries - buffer should keep working
        for i in 0..100u64 {
            buf.push(i, DataDirection::Tx, &(i as u32).to_le_bytes());
        }

        // Should still be functional
        assert!(!buf.is_empty());
        let (header, _) = buf.pop().unwrap();
        // The last few entries should survive
        assert!(header.timestamp >= 97);
    }

    #[test]
    fn test_direction_preserved() {
        let mut buf = small_buffer(256);

        buf.push(1, DataDirection::Tx, b"tx");
        buf.push(2, DataDirection::Rx, b"rx");
        buf.push(3, DataDirection::Tx, b"tx");

        let directions: Vec<DataDirection> = (0..3)
            .map(|_| buf.pop().unwrap().0.data_direction().unwrap())
            .collect();

        assert_eq!(
            directions,
            vec![DataDirection::Tx, DataDirection::Rx, DataDirection::Tx]
        );
    }

    #[test]
    fn test_timestamps_preserved_fifo() {
        let mut buf = small_buffer(512);

        let timestamps = [100u64, 200, 300, 400, 500];
        for &ts in &timestamps {
            buf.push(ts, DataDirection::Tx, b"x");
        }

        for &expected_ts in &timestamps {
            let (header, _) = buf.pop().unwrap();
            assert_eq!(header.timestamp, expected_ts);
        }
    }
}
