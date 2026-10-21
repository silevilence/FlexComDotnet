//! Capture/session engine shared by the real WDM dispatch path and host tests.
//! All calls require external serialization. No allocation occurs after `new`.
#[cfg(feature = "kernel")]
use alloc::vec::Vec;

pub const VERSION: u32 = 2;
pub const HEADER_SIZE: usize = 64;
pub const PAYLOAD_SIZE: usize = 4096;
pub const RECORD_SIZE: usize = HEADER_SIZE + PAYLOAD_SIZE;
pub const STATUS_SIZE: usize = 64;
pub const MAX_PORTS: usize = 32;
pub const FLAG_DATA_UNAVAILABLE: u16 = 1;
pub const FLAG_MORE: u16 = 2;
pub const FLAG_SESSION_BOUNDARY: u16 = 4;
pub const STATUS_INVALID_PARAMETER: i32 = 0xC000000Du32 as i32;
pub const STATUS_BUFFER_TOO_SMALL: i32 = 0xC0000023u32 as i32;
pub const STATUS_NOT_FOUND: i32 = 0xC0000225u32 as i32;
pub const STATUS_ACCESS_DENIED: i32 = 0xC0000022u32 as i32;
pub const STATUS_BUSY: i32 = 0x80000011u32 as i32;
pub const STATUS_NO_DATA: i32 = 0x8000001Au32 as i32;
pub const STATUS_NO_MEMORY: i32 = 0xC000009Au32 as i32;
// A new function range prevents an old driver from accepting the v2 contract.
pub const IOCTL_START: u32 = 0x8000E040;
pub const IOCTL_STOP: u32 = 0x8000E044;
pub const IOCTL_DATA: u32 = 0x80006048;
pub const IOCTL_STATUS: u32 = 0x8000604C;
pub const IOCTL_DIAGNOSTICS: u32 = 0x80006050;

pub fn validate_ioctl(code: u32, input: usize, output: usize) -> Result<(), i32> {
    let (required_in, required_out) = match code {
        IOCTL_START => (128, 0),
        IOCTL_STOP => (0, 0),
        IOCTL_DATA => (0, RECORD_SIZE),
        IOCTL_STATUS => (0, STATUS_SIZE),
        IOCTL_DIAGNOSTICS => (0, 20),
        _ => return Err(STATUS_INVALID_PARAMETER),
    };
    if input < required_in || output < required_out {
        Err(STATUS_BUFFER_TOO_SMALL)
    } else {
        Ok(())
    }
}

#[repr(C)]
#[derive(Clone, Copy, Debug, Default)]
pub struct RecordHeader {
    pub timestamp: u64,
    pub sequence: u64,
    pub request_id: u64,
    pub port_id: u64,
    pub status: i32,
    pub ioctl: u32,
    pub requested_length: u32,
    pub transferred_length: u32,
    pub data_length: u32,
    pub offset: u32,
    pub direction: u8,
    pub kind: u8,
    pub flags: u16,
}

impl RecordHeader {
    pub fn encode(&self, bytes: &mut [u8]) {
        bytes[..HEADER_SIZE].fill(0);
        for (i, value) in [self.timestamp, self.sequence, self.request_id, self.port_id]
            .iter()
            .enumerate()
        {
            bytes[i * 8..i * 8 + 8].copy_from_slice(&value.to_le_bytes());
        }
        for (i, value) in [
            self.status as u32,
            self.ioctl,
            self.requested_length,
            self.transferred_length,
            self.data_length,
            self.offset,
        ]
        .iter()
        .enumerate()
        {
            bytes[32 + i * 4..36 + i * 4].copy_from_slice(&value.to_le_bytes());
        }
        bytes[56] = self.direction;
        bytes[57] = self.kind;
        bytes[58..60].copy_from_slice(&self.flags.to_le_bytes());
    }

    pub fn decode(bytes: &[u8]) -> Option<Self> {
        if bytes.len() < HEADER_SIZE {
            return None;
        }
        let u64_at = |i| u64::from_le_bytes(bytes[i..i + 8].try_into().unwrap_or_default());
        let u32_at = |i| u32::from_le_bytes(bytes[i..i + 4].try_into().unwrap_or_default());
        Some(Self {
            timestamp: u64_at(0),
            sequence: u64_at(8),
            request_id: u64_at(16),
            port_id: u64_at(24),
            status: u32_at(32) as i32,
            ioctl: u32_at(36),
            requested_length: u32_at(40),
            transferred_length: u32_at(44),
            data_length: u32_at(48),
            offset: u32_at(52),
            direction: bytes[56],
            kind: bytes[57],
            flags: u16::from_le_bytes([bytes[58], bytes[59]]),
        })
    }
}

#[derive(Clone, Copy)]
pub struct CaptureToken {
    generation: u64,
    pub port_id: u64,
    pub request_id: u64,
}

#[derive(Clone, Copy)]
pub struct EventInfo {
    pub flags: u16,
    pub direction: u8,
    pub kind: u8,
    pub requested: u32,
    pub transferred: u32,
    pub status: i32,
    pub timestamp: u64,
    pub ioctl: u32,
}

impl EventInfo {
    pub fn data(
        direction: u8,
        requested: u32,
        transferred: u32,
        status: i32,
        timestamp: u64,
    ) -> Self {
        Self {
            flags: 0,
            direction,
            kind: 0,
            requested,
            transferred: transferred.min(requested),
            status,
            timestamp,
            ioctl: 0,
        }
    }
}

#[derive(Default, Clone, Copy)]
pub struct CaptureStatus {
    pub state: u32,
    pub port_id: u64,
    pub generation: u64,
    pub total_bytes_rx: u64,
    pub total_bytes_tx: u64,
    pub dropped_records: u64,
    pub dropped_bytes: u64,
    pub queued_records: u32,
    pub attached_ports: u32,
}

impl CaptureStatus {
    pub fn encode(&self, bytes: &mut [u8]) {
        bytes[..4].copy_from_slice(&VERSION.to_le_bytes());
        bytes[4..8].copy_from_slice(&self.state.to_le_bytes());
        for (i, value) in [
            self.port_id,
            self.generation,
            self.total_bytes_rx,
            self.total_bytes_tx,
            self.dropped_records,
            self.dropped_bytes,
        ]
        .iter()
        .enumerate()
        {
            bytes[8 + i * 8..16 + i * 8].copy_from_slice(&value.to_le_bytes());
        }
        bytes[56..60].copy_from_slice(&self.queued_records.to_le_bytes());
        bytes[60..64].copy_from_slice(&self.attached_ports.to_le_bytes());
    }
}

struct Port {
    id: u64,
    name: [u16; 64],
}
struct Record {
    header: RecordHeader,
    data: [u8; PAYLOAD_SIZE],
}

pub struct MonitorCore {
    ports: Vec<Port>,
    records: Vec<Record>,
    read: usize,
    count: usize,
    owner: u64,
    next_request: u64,
    sequence: u64,
    status: CaptureStatus,
}

impl MonitorCore {
    pub fn new(capacity: usize) -> Result<Self, i32> {
        if capacity == 0 {
            return Err(STATUS_INVALID_PARAMETER);
        }
        let mut records = Vec::new();
        records
            .try_reserve_exact(capacity)
            .map_err(|_| STATUS_NO_MEMORY)?;
        for _ in 0..capacity {
            records.push(Record {
                header: RecordHeader::default(),
                data: [0; PAYLOAD_SIZE],
            });
        }
        let mut ports = Vec::new();
        ports
            .try_reserve_exact(MAX_PORTS)
            .map_err(|_| STATUS_NO_MEMORY)?;
        Ok(Self {
            ports,
            records,
            read: 0,
            count: 0,
            owner: 0,
            next_request: 0,
            sequence: 0,
            status: CaptureStatus::default(),
        })
    }

    pub fn register_port(&mut self, id: u64, name: [u16; 64]) -> Result<(), i32> {
        if id == 0 || name[0] == 0 || !name.contains(&0) {
            return Err(STATUS_INVALID_PARAMETER);
        }
        if self.ports.len() == MAX_PORTS {
            return Err(STATUS_NO_MEMORY);
        }
        if self.ports.iter().any(|p| p.id == id) {
            return Err(STATUS_INVALID_PARAMETER);
        }
        self.ports.push(Port { id, name });
        Ok(())
    }

    pub fn unregister_port(&mut self, id: u64) {
        self.ports.retain(|p| p.id != id);
        if self.status.port_id == id {
            self.status.state = 0;
            self.status.generation = self.status.generation.wrapping_add(1);
        }
    }

    pub fn start(&mut self, owner: u64, name: &[u16; 64]) -> Result<(), i32> {
        if owner == 0 || name[0] == 0 || !name.contains(&0) {
            return Err(STATUS_INVALID_PARAMETER);
        }
        if self.status.state != 0 {
            return Err(STATUS_BUSY);
        }
        let fold = |c: u16| if (65..=90).contains(&c) { c + 32 } else { c };
        let id = self
            .ports
            .iter()
            .find(|p| {
                p.name
                    .iter()
                    .map(|c| fold(*c))
                    .eq(name.iter().map(|c| fold(*c)))
            })
            .map(|p| p.id)
            .ok_or(STATUS_NOT_FOUND)?;
        let generation = self.status.generation.wrapping_add(1);
        self.status = CaptureStatus {
            state: 1,
            port_id: id,
            generation,
            ..Default::default()
        };
        self.owner = owner;
        self.read = 0;
        self.count = 0;
        self.sequence = 0;
        Ok(())
    }

    pub fn stop(&mut self, owner: u64) -> Result<(), i32> {
        if self.owner != 0 && owner != self.owner {
            return Err(STATUS_ACCESS_DENIED);
        }
        self.status.state = 0;
        self.status.generation = self.status.generation.wrapping_add(1);
        Ok(())
    }

    pub fn cleanup(&mut self, owner: u64) {
        if self.owner == owner {
            let _ = self.stop(owner);
            self.owner = 0;
            self.count = 0;
        }
    }

    pub fn begin(&mut self, port_id: u64) -> Option<CaptureToken> {
        if self.status.state == 0 || self.status.port_id != port_id {
            return None;
        }
        self.next_request = self.next_request.wrapping_add(1);
        Some(CaptureToken {
            generation: self.status.generation,
            port_id,
            request_id: self.next_request,
        })
    }

    fn accepts(&self, token: CaptureToken) -> bool {
        self.status.state != 0
            && token.generation == self.status.generation
            && token.port_id == self.status.port_id
    }

    pub fn lost(&mut self, token: CaptureToken, bytes: u32) {
        if self.accepts(token) {
            // Reserve a missing sequence number so framing cannot bridge this gap.
            self.sequence += 1;
            self.status.dropped_records += 1;
            self.status.dropped_bytes += u64::from(bytes);
        }
    }

    /// Observes completion-time data even when a read was pending before START.
    /// Such records are marked as crossing a session boundary, never silently
    /// treated as an entire protocol frame belonging to the new session.
    pub fn observe_completion(
        &mut self,
        original: Option<CaptureToken>,
        port_id: u64,
        mut info: EventInfo,
        data: Option<&[u8]>,
    ) {
        let token = match original.filter(|t| self.accepts(*t)) {
            Some(token) => token,
            None => {
                let Some(token) = self.begin(port_id) else {
                    return;
                };
                info.flags |= FLAG_SESSION_BOUNDARY;
                token
            }
        };
        self.complete(token, info, data);
    }

    pub fn complete(&mut self, token: CaptureToken, info: EventInfo, data: Option<&[u8]>) {
        if !self.accepts(token) {
            return;
        }
        if info.kind == 0 {
            if info.direction == 0 {
                self.status.total_bytes_tx += u64::from(info.transferred);
            }
            if info.direction == 1 {
                self.status.total_bytes_rx += u64::from(info.transferred);
            }
        }
        let payload = data.unwrap_or_default();
        let payload = if info.kind == 0 {
            &payload[..payload.len().min(info.transferred as usize)]
        } else {
            payload
        };
        let chunks = payload.len().max(1).div_ceil(PAYLOAD_SIZE);
        // Bound work under the spin lock to one queue's worth, even for huge IRPs.
        let skipped = chunks.saturating_sub(self.records.len());
        self.status.dropped_records += skipped as u64;
        self.status.dropped_bytes += (skipped * PAYLOAD_SIZE) as u64;
        self.sequence += skipped as u64;
        for chunk in skipped..chunks {
            if self.count == self.records.len() {
                self.status.dropped_records += 1;
                self.status.dropped_bytes += u64::from(self.records[self.read].header.data_length);
                self.read = (self.read + 1) % self.records.len();
                self.count -= 1;
            }
            let offset = chunk * PAYLOAD_SIZE;
            let length = PAYLOAD_SIZE.min(payload.len().saturating_sub(offset));
            let index = (self.read + self.count) % self.records.len();
            self.sequence += 1;
            let unavailable = info.transferred > payload.len() as u32 && info.kind == 0;
            self.records[index].header = RecordHeader {
                timestamp: info.timestamp,
                sequence: self.sequence,
                request_id: token.request_id,
                port_id: token.port_id,
                status: info.status,
                ioctl: info.ioctl,
                requested_length: info.requested,
                transferred_length: info.transferred,
                data_length: length as u32,
                offset: offset as u32,
                direction: info.direction,
                kind: info.kind,
                flags: info.flags
                    | if unavailable {
                        FLAG_DATA_UNAVAILABLE
                    } else {
                        0
                    }
                    | if chunk + 1 < chunks { FLAG_MORE } else { 0 },
            };
            self.records[index].data[..length].copy_from_slice(&payload[offset..offset + length]);
            self.count += 1;
        }
    }

    pub fn read(&mut self, owner: u64, output: &mut [u8]) -> Result<usize, i32> {
        if owner != self.owner {
            return Err(STATUS_ACCESS_DENIED);
        }
        if self.count == 0 {
            return Err(STATUS_NO_DATA);
        }
        let record = &self.records[self.read];
        let length = record.header.data_length as usize;
        if output.len() < HEADER_SIZE + length {
            return Err(STATUS_BUFFER_TOO_SMALL);
        }
        record.header.encode(output);
        output[HEADER_SIZE..HEADER_SIZE + length].copy_from_slice(&record.data[..length]);
        self.read = (self.read + 1) % self.records.len();
        self.count -= 1;
        Ok(HEADER_SIZE + length)
    }

    pub fn status(&self) -> CaptureStatus {
        CaptureStatus {
            queued_records: self.count as u32,
            attached_ports: self.ports.len() as u32,
            ..self.status
        }
    }
}
