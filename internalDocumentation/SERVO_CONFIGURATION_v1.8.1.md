# Servo Configuration v1.8.1

- The top-level **File** menu uses a lighter foreground for visibility.
- The Maestro channel column is labeled **Port** and is half the prior width.
- Default/Min/Max and individual Speeds/Accels fields are 70% of their prior widths.
- Ganged Speeds/Accels fields are also reduced to 70% of their prior width.
- Port entry accepts only Maestro channels 0 through 23; invalid typing/paste is rejected and the data model also clamps loaded values to 0..23.
- The repeated NeckTiltLeft/NeckTiltRight physical-servo rows under NeckTiltRight and NeckNodUp stay synchronized because they share the same physical ServoConfigEntry. Their existing default ports remain NeckTiltLeft=4 and NeckTiltRight=13.
- If two different physical servos use the same Maestro port, the Port and RobotControl text are shown bold bright red. The repeated neck rows do not count as duplicate physical servos and therefore do not create false warnings.
