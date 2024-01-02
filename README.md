# PingPang
Virtual reality ping pong aiming for maximum physics fidelity.

Uses Unity XR and targets standalone Meta Quest headsets.

## Problems

- Inaccurate hand controller tracking.  This has been the biggest obstacle.  The tracking during fast motions is frequently wrong by more than 10 cm, while the typical table tennis paddle face radius is about 8 cm.  So a motion that should strike the ball in the middle of the paddle completely misses the ball in the VR simulation.

- Hand controller acceleration limit.  Hand controllers on all headsets I have tried prior to Quest 3 have an inertial measurement unit with maximum acceleration of 16g.  It is quite easy to exceed that acceleration in which case tracking is lost producing the wrong paddle motion.  Testing shows that all of these hand controllers have this severe limitation: Meta Quest Pro, Meta Quest 2, Valve Index, Oculus Rift S, Vive Pro, Vive, Oculus Rift.  It appears the Meta Quest 3 is the first system I have tried that has inertial measurement unit with a higher lijmit of 32g from the IMU data sheet.  Testing with this ping pong applications appears to show much more faithful tracking at high accelerations.

- Limited field of view.  Surprisingly the 100 degree field of view of many VR headsets is a very noticeable limitation playing ping pong.  Looking at photos of professional table tennis players serving shows they often are looking at the ball with eye pupils far from the center of the eye.  That makes sense since fast head motions to track the ball would be impractical.

- Limited frame rate.  The ping pong ball often travels faster than 10 meters per second.  At the typical 72 - 144 Hz refresh rate of VR headsets the ball moves about 10 cm between frames when moving at 10 m/s.  The ball is only 4 cm in diameter, so the motion has the ball jumping with gaps of 6 cm each frame.  This is not visually too apparent, because the brain adapts to it.  But I think it creates subconcious discomfort, possibly because the eye retina no longer perceives a continuous motion and higher level brain processing makes it appear smooth.

Developed since 2016.