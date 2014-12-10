RandomVideoScreenSaver
======================

Windows screen saver that plays random videos within a directory

* Accepts MP4, MOV, JPG, or PNG files (see GetAllVideos)
* Caches paths in %AppData%\Local\VideoScreenSaver\ScreenSaver.data.bin
* (TO DO) - update this in a background thread
* Tries to get EXIF and title information from the file and displays that
* Accepts commands while image is up - D to delete, I to show info, C to copy
	* Copies path name as TEXT and image as Bitmap to clipboard
* Log4Net logging to %ProgramData%\VideoScreenSaver-Log.txt according to 
  options set in the .app.config file
* Post-build command to create a single exe with depedent DLLs using ILMerge
  and rename it as a .SCR which is what Windows expects a screen saver to be
  named.  Just copy to %WINDIR%\system32 these files:
	- VideoScreenSaver.SCR (the ILMerge'd EXE)
	- VideoScreenSaver.SCR.Config (the app.config)
	- VideoScreenSaver.PDB (if desired)

