# Capture Tools 1.3.0

Capture tools is paid software that took a lot work to create, please don't send it to anyone who hasn't paid for it. I'd be very grateful if you could refer anyone who might be interested to the gumroad page. Thank you.

https://halban.gumroad.com/l/CaptureTools


# Changelog

## 1.3.0

- Fixed a bug that was causing the main and multi cameras to trail one physics update behind their target positions. This is what was causing fast vessels to sometimes leave the frame in multicam views with PRE installed; main camera tracking should also be much tighter now at high speeds.

- Revamped and refactored multicam camera movement to smoothly transition between states and targets while keeping both vessels in frame at all times. The movement should look more similar to the main camera.

- Fixed a bug where multicam would always record even when 'Preview Only' was enabled.


## 1.2.0

- Added integration with Scatterer to support Scatterer's TAA/SMAA and sunlight colour tinting in main cam and multi cam outputs.

- The UI toggle keybind now starts a main camera recording when combined with right alt. So by default, it's right alt + F8.

- Added a toggle to enable/disable positional smoothing of the main camera. This is referring to the pivot point that the camera orbits around when you click and drag, not the position of the camera itself.

- Fixed a bug where smoothing would break when smoothing time was set too low.


## 1.1.0

- Added an option to draw the flight UI to the main camera output. This method draws the flight UI canvas exactly as it looks on your screen, so 3D icons/labels won't line up exactly when using smoothing, and it won't include most mod windows.

- Added an option to only record audio. This can be used alongside an external recorder and the decimator script to record lag-free footage with sound that includes the flight UI, all mod windows and external post-processing effects like reshade. When you start recording in audio only mode, a sync marker will flash on screen to show you where to place the audio when editing.

- Removed some unnecessary logging.
