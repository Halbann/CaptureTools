# Capture Tools 1.4.0

Capture tools is paid software that took a lot work to create, please don't send it to anyone who hasn't paid for it. I'd be very grateful if you could refer anyone who might be interested to the gumroad page. Thank you.
https://halban.gumroad.com/l/CaptureTools

The owner's manual covers everything you need to know about using Capture Tools and will hopefully answer any questions you may have. It takes about 15 minutes to read: 
https://github.com/Halbann/CaptureTools/wiki


# Changelog

## 1.4.0

- Fixed the black frame appearing at the beginning of video files.
- Fixed EVE volumetric clouds not appearing in the first frame of video files.
- Fixed a bug where the focus of the flight camera rotating suddenly would cause the main camera to lose its orientation.
- Fixed a bug where the atmosphere would flicker with Scatterer and Texture Replacer's real time reflections enabled.
- Fixed various bugs with Deferred integration (blurry terrain, ambient light, motion blur artefacts).
- Fixed Scatterer sunflare and sunlight colour modulation.
- Smoothing can be set to zero, in which case smoothing will be skipped (previously had a small minimum).

## 1.3.0

- Added an owner's manual, available at https://github.com/Halbann/CaptureTools/wiki.

- Added a setting called 'Target Delay' to multicam that allows you to add a small delay before the camera switches away from a BDA target. This is to make sure that BDA kills are recorded. I'm intending to extend this to Kessler in future.

- Added Scatterer sunflares to main and multicam outputs. Currently only supports post-volumetric clouds versions of Scatterer.

- Revamped multicam camera movement to smoothly transition between states and targets while doing a better job of keeping both vessels in the frame. The movement should look more similar to the main camera.

- Fixed a bug that was causing the main and multi cameras to trail one physics update behind their target positions. This is what was causing fast moving vessels to sometimes leave the frame (especially with PRE installed), even with very low smoothing times.

- Fixed a bug where multicam would record even when 'Preview Only' was enabled.

- Fixed a bug where the part under the mouse when starting a capture would be stuck as highlighted.

- Fixed a bug where part highlighters would reappear during a recording after hiding and showing the UI.

- Fixed a bug where the main camera would look in the wrong direction when transitioning between vessels in space.


## 1.2.0

- Added integration with Scatterer to support Scatterer's TAA/SMAA and sunlight colour tinting in main cam and multi cam outputs.

- The UI toggle keybind now starts a main camera recording when combined with right alt. So by default, it's right alt + F8.

- Added a toggle to enable/disable positional smoothing of the main camera. This is referring to the pivot point that the camera orbits around when you click and drag, not the position of the camera itself.

- Fixed a bug where smoothing would break when smoothing time was set too low.


## 1.1.0

- Added an option to draw the flight UI to the main camera output. This method draws the flight UI canvas exactly as it looks on your screen, so 3D icons/labels won't line up exactly when using smoothing, and it won't include most mod windows.

- Added an option to only record audio. This can be used alongside an external recorder and the decimator script to record lag-free footage with sound that includes the flight UI, all mod windows and external post-processing effects like reshade. When you start recording in audio only mode, a sync marker will flash on screen to show you where to place the audio when editing.

- Removed some unnecessary logging.
