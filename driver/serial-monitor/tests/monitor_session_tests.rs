use serial_monitor::monitor::*;

#[test]
fn v2_wire_contract_uses_fixed_offsets_and_clears_reserved_bytes() {
    assert_eq!(core::mem::size_of::<RecordHeader>(), HEADER_SIZE);
    assert_eq!(core::mem::offset_of!(RecordHeader, status), 32);
    assert_eq!(core::mem::offset_of!(RecordHeader, data_length), 48);
    assert_eq!(core::mem::offset_of!(RecordHeader, direction), 56);
    let header = RecordHeader {
        timestamp: 123,
        port_id: 7,
        status: -1,
        data_length: 3,
        direction: 1,
        ..Default::default()
    };
    let mut bytes = [0xFF; HEADER_SIZE];
    header.encode(&mut bytes);
    assert_eq!(&bytes[60..64], &[0; 4]);
    assert_eq!(RecordHeader::decode(&bytes).unwrap().port_id, 7);
}

#[test]
fn completed_config_and_failed_io_preserve_status_without_inventing_data() {
    let mut core = core();
    core.start(1, &name("\\Device\\USBPDO-1")).unwrap();
    let token = core.begin(10).unwrap();
    core.complete(
        token,
        EventInfo {
            flags: 0,
            direction: 2,
            kind: 1,
            requested: 4,
            transferred: 0,
            status: 0,
            timestamp: 20,
            ioctl: 0x1B0004,
        },
        Some(&115200u32.to_le_bytes()),
    );
    let mut output = vec![0; RECORD_SIZE];
    core.read(1, &mut output).unwrap();
    assert_eq!(RecordHeader::decode(&output).unwrap().ioctl, 0x1B0004);
    assert_eq!(
        &output[HEADER_SIZE..HEADER_SIZE + 4],
        &115200u32.to_le_bytes()
    );
    let token = core.begin(10).unwrap();
    core.complete(
        token,
        EventInfo::data(0, 20, 0, 0xC0000120u32 as i32, 30),
        None,
    );
    core.read(1, &mut output).unwrap();
    let header = RecordHeader::decode(&output).unwrap();
    assert_eq!(header.status as u32, 0xC0000120);
    assert_eq!(header.data_length, 0);
    assert_eq!(core.status().total_bytes_tx, 0);
}

fn name(value: &str) -> [u16; 64] {
    let mut result = [0; 64];
    for (slot, ch) in result.iter_mut().zip(value.encode_utf16()) {
        *slot = ch;
    }
    result
}

fn core() -> MonitorCore {
    let mut core = MonitorCore::new(8).unwrap();
    core.register_port(10, name("\\Device\\USBPDO-1")).unwrap();
    core.register_port(20, name("\\Device\\USBPDO-2")).unwrap();
    core
}

#[test]
fn start_requires_the_selected_attached_device() {
    let mut core = core();
    assert_eq!(
        core.start(1, &name("\\Device\\missing")),
        Err(STATUS_NOT_FOUND)
    );
    core.start(1, &name("\\device\\usbpdo-1")).unwrap();
    assert!(core.begin(20).is_none());
    assert!(core.begin(10).is_some());
}

#[test]
fn pending_read_can_be_observed_after_start_but_is_marked_as_a_boundary() {
    let mut core = core();
    core.start(1, &name("\\Device\\USBPDO-1")).unwrap();
    core.observe_completion(None, 20, EventInfo::data(1, 1, 1, 0, 1), Some(b"z"));
    assert_eq!(core.status().queued_records, 0);
    core.observe_completion(None, 10, EventInfo::data(1, 1, 1, 0, 1), Some(b"x"));
    let mut output = vec![0; RECORD_SIZE];
    core.read(1, &mut output).unwrap();
    assert_ne!(
        RecordHeader::decode(&output).unwrap().flags & FLAG_SESSION_BOUNDARY,
        0
    );
    core.stop(1).unwrap();
    core.observe_completion(None, 10, EventInfo::data(1, 1, 1, 0, 1), Some(b"y"));
    assert_eq!(core.status().queued_records, 0);
}

#[test]
fn stop_and_restart_discard_late_completions_from_the_old_session() {
    let mut core = core();
    assert!(core.begin(10).is_none());
    core.start(1, &name("\\Device\\USBPDO-1")).unwrap();
    let old = core.begin(10).unwrap();
    core.stop(1).unwrap();
    assert!(core.begin(10).is_none());
    core.start(1, &name("\\Device\\USBPDO-1")).unwrap();
    core.complete(old, EventInfo::data(0, 2, 2, 0, 100), Some(b"old"));
    assert_eq!(core.status().queued_records, 0);
}

#[test]
fn cleanup_is_owned_and_removal_invalidates_capture() {
    let mut core = core();
    core.start(1, &name("\\Device\\USBPDO-1")).unwrap();
    assert_eq!(core.stop(2), Err(STATUS_ACCESS_DENIED));
    core.cleanup(2);
    assert!(core.begin(10).is_some());
    core.unregister_port(10);
    assert_eq!(core.status().state, 0);
    assert!(core.begin(10).is_none());
}

#[test]
fn large_transfer_roundtrips_in_bounded_chunks_with_metadata() {
    let mut core = core();
    core.start(1, &name("\\Device\\USBPDO-1")).unwrap();
    let token = core.begin(10).unwrap();
    let data: Vec<u8> = (0..10000).map(|i| (i % 251) as u8).collect();
    core.complete(token, EventInfo::data(1, 12000, 10000, 0, 123), Some(&data));
    let mut recovered = Vec::new();
    let mut output = vec![0; RECORD_SIZE];
    for offset in [0, 4096, 8192] {
        let count = core.read(1, &mut output).unwrap();
        let header = RecordHeader::decode(&output).unwrap();
        assert_eq!(header.offset, offset);
        assert_eq!(header.port_id, 10);
        assert_eq!(header.request_id, token.request_id);
        assert_eq!(header.requested_length, 12000);
        assert_eq!(header.transferred_length, 10000);
        assert_eq!(count, HEADER_SIZE + header.data_length as usize);
        recovered.extend_from_slice(&output[HEADER_SIZE..count]);
    }
    assert_eq!(recovered, data);
    assert_eq!(core.status().total_bytes_rx, 10000);
}

#[test]
fn short_output_does_not_consume_a_record() {
    let mut core = core();
    core.start(1, &name("\\Device\\USBPDO-1")).unwrap();
    let token = core.begin(10).unwrap();
    core.complete(token, EventInfo::data(0, 1, 1, 0, 1), Some(b"x"));
    assert_eq!(
        core.read(1, &mut [0; HEADER_SIZE]),
        Err(STATUS_BUFFER_TOO_SMALL)
    );
    assert_eq!(core.status().queued_records, 1);
}

#[test]
fn overflow_and_unavailable_buffers_are_visible() {
    let mut core = MonitorCore::new(1).unwrap();
    core.register_port(10, name("p")).unwrap();
    core.start(1, &name("p")).unwrap();
    let token = core.begin(10).unwrap();
    core.complete(
        token,
        EventInfo::data(0, 5000, 5000, 0, 1),
        Some(&vec![7; 5000]),
    );
    assert_eq!(core.status().dropped_records, 1);
    assert_eq!(core.status().dropped_bytes, 4096);
    let token = core.begin(10).unwrap();
    core.complete(token, EventInfo::data(1, 8, 8, 0, 2), None);
    let mut output = vec![0; RECORD_SIZE];
    core.read(1, &mut output).unwrap();
    let header = RecordHeader::decode(&output).unwrap();
    assert_ne!(header.flags & FLAG_DATA_UNAVAILABLE, 0);
    assert_eq!(header.transferred_length, 8);
}

#[test]
fn ioctl_sizes_and_unknown_codes_are_rejected_before_access() {
    assert_eq!(
        validate_ioctl(IOCTL_START, 2, 0),
        Err(STATUS_BUFFER_TOO_SMALL)
    );
    assert_eq!(
        validate_ioctl(IOCTL_STATUS, 0, STATUS_SIZE - 1),
        Err(STATUS_BUFFER_TOO_SMALL)
    );
    assert_eq!(
        validate_ioctl(IOCTL_DATA, 0, 4),
        Err(STATUS_BUFFER_TOO_SMALL)
    );
    assert_eq!(validate_ioctl(0, 4096, 4096), Err(STATUS_INVALID_PARAMETER));
    assert!(validate_ioctl(IOCTL_START, 128, 0).is_ok());
}

#[test]
fn capture_failure_leaves_a_sequence_gap_for_frame_consumers() {
    let mut core = core();
    core.start(1, &name("\\Device\\USBPDO-1")).unwrap();
    let token = core.begin(10).unwrap();
    core.lost(token, 20);
    let token = core.begin(10).unwrap();
    core.complete(token, EventInfo::data(0, 1, 1, 0, 1), Some(b"x"));
    let mut output = vec![0; RECORD_SIZE];
    core.read(1, &mut output).unwrap();
    assert_eq!(RecordHeader::decode(&output).unwrap().sequence, 2);
    assert_eq!(core.status().dropped_records, 1);
}

#[test]
fn serial_handflow_code_matches_wdk_set_operation() {
    assert!(serial_monitor::filter::is_monitored_serial_ioctl(
        0x001B0064
    ));
    assert!(!serial_monitor::filter::is_monitored_serial_ioctl(
        0x001B0060
    ));
}
