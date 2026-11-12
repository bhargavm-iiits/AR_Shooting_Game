# Tracking diagnostics — 11 November 2026

The captured game log contains direct evidence of ARCore visual-inertial tracking faults and map-coordinate corrections while camera and motion samples continue arriving. It does not establish dead sensors, a calibration defect, or stationary-phone drift.

Source: `Device-BeforeRestart-logcat.txt` captured by `Device-BeforeRestartSmoke.json` at 10:33:10 UTC, game PID **7422**, CPH2487 / Android 16 / ARCore 1.57.262640293. Installed APK SHA-256: `F675A6EAAE02BB5D50E16D3F0B92E71F8999FD26ED9B37DB78C50DCC0939CBA3`. The reviewed log SHA-256 is `8A19BE7A512A7431A6485B7BBF36642A8D52BE8B3598667BCB533B28CF7FC15C`; a byte-identical snapshot is retained at `tmp/device-health/tracking-pid7422-F675-logcat.txt`. Line numbers below refer to this snapshot, counting newline characters normally.

## Pose discontinuity within the available AR sequence

There are **17** Unity pose diagnostics in the available buffer, all from the same process, all reporting `SessionTracking`, `reason=None`, and identical world/local camera positions. There are **zero** `Reality Defender state` messages or explicit app restart/menu transitions in this captured interval. The buffer begins after AR startup, so this is an uninterrupted *observed log interval*, not proof of the entire session's history.

The largest adjacent sampled displacement is **24.9204 m**:

| Snapshot line | Approximate IST time from adjacent native logs | Unity world and local camera position |
| --- | --- | --- |
| 698 | 16:03:02.46 | `(24.268, -1.767, 5.390)` |
| 778 | 16:03:03.46 | `(0.007, 0.103, 0.011)` |

The source logs poses about once per second; Unity lines themselves have no timestamp in this brief logcat format. The one-second interval is approximate. Absolute camera coordinates reach roughly 26 m earlier in the buffer, but this is **not** a measured 26 m user walk or a stationary-device drift measurement.

Between these two samples, native ARCore logs explicitly record:

- Line 751, 16:03:02.960: resetting `adf_t_vio`, with a large translation correction.
- Line 762, 16:03:03.025: localization to a new map/optimization.
- Line 763, the same instant: changing the world-pose node from one node ID to another.

This is strong evidence that the discontinuity includes an ARCore coordinate-frame correction. It is not merely inferred from the size of the motion, and no user-triggered app restart is logged across it. Real physical movement and environmental conditions were not independently measured.

## Native tracking and timing evidence

- Lines 202–207 at 16:02:55.716–55.733 explicitly report `VIO_STATE_ANOMALY`, resetting VIO with `faults: 3`, `VIO_RESET`, output not tracking, and resetting the map. Initialization fails once at line 222 and succeeds at line 259 about 0.22 seconds after the fault. The 1 Hz Unity diagnostic can miss this brief native loss; sampled `reason=None` does not establish uninterrupted reliable tracking.
- The buffer contains **24** insufficient-inlier/feature-ratio messages, **8** insufficient RANSAC-inlier messages, and **3** messages about estimated motion with failed frame-to-frame translation.
- There are **9** `FEATURE_DSP_CAM_IMU_DESYNC` messages: logged camera-to-IMU offsets range from **−10.102 to −18.784 ms**, exceeding the provider's logged **5 ms** threshold. No timestamp-out-of-order message appears in this specific buffer. The timing warnings coexist with the VIO faults; the log alone does not prove which caused which or identify a firmware defect.
- Native delivery counters increase: camera **760→1247**, gyro **5200→8400**, accelerometer **5199→8398**, across approximately 16 seconds. This independently demonstrates continuing input delivery during the problematic run; it does not certify timing calibration.

## What remains unverified

All 17 diagnostics show `anchor=none`, `reticle=hidden`, and `plane=none`. Here `plane` is the controller's **selected candidate**, not a count of every provider plane. No placed-anchor stability result can be inferred from this capture. `inputPosition=unavailable` refers to the legacy XR-node diagnostic query; it does not establish that the Input System pose action failed. The Unity camera does change position and rotation, and world/local equality gives no indication of an extra origin translation in these samples. A direct comparison with resolved Input System controls remains necessary to isolate any camera-pose wiring error.

Earlier live service captures at 15:53:21 IST also showed an ACTIVE game camera client, surviving cameraserver PID 2924, and advancing uncalibrated accel/gyro timestamps (1458.628→1461.726 and 1458.631→1461.729 seconds). Those observations were recorded during the audit; subsequent diagnostics overwrote the earlier summary files, so they should not be presented as a currently intact complete JSON capture. The present PID7422 delivery counters provide independently retained evidence. A later failed health capture coincided with the root agent's observation that Unity shutdown stopped the host ADB daemon; empty ADB output is not evidence of a phone camera/sensor crash.

This review made no device, application, runtime-code, or calibration changes.
