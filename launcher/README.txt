PULL FROM THE CLOUD CALCULATOR
==============================

Tells you whether an incident report can be pulled from the cloud.
A report can be pulled once 6 hours have passed since it was created (US time).


HOW TO START
------------
1. Unzip the WHOLE PullCloud folder somewhere local, e.g. C:\Tools\PullCloud
   (preferably not inside OneDrive). Keep the folder structure as it is.
2. Double-click "Start PullCloud.cmd".
3. Optional, recommended: double-click "Create desktop shortcut.cmd" once.
   You then get a "Pull from the Cloud" icon on your Desktop that opens the app
   directly. (If you move the PullCloud folder later, run it again.)

Nothing needs to be installed and no admin rights are required: the folder
contains its own copy of Microsoft's .NET runtime (in "runtime"). The app starts
through Microsoft's signed dotnet.exe, so it opens in about a second and is not
held up by the company antivirus check for unknown .exe files.


HOW TO USE
----------
1. INCIDENT CREATED ON - the creation date/time exactly as shown in the US system
   (MM/dd/yyyy, hour, minutes, AM/PM).
   Or paste it into "...or paste from the system", e.g. 11/26/2026 3:45 PM EST
2. STATE - required.
3. CITY - optional. Needed only for states with more than one time zone
   (Indiana, Tennessee, Florida, Kentucky, Texas, Michigan, Kansas, Nebraska,
   North/South Dakota, Oregon, Idaho, Nevada, Arizona, Alaska).
   Without a city the app uses the safest (latest) possible pull time.
4. MY LOCATION - pick your city once and click Save. It is remembered.

RESULT
  PULL AVAILABLE  - 6+ hours have passed, the report can be pulled.
  WAIT            - less than 6 hours; pull is close (default: within 60 min).
                    Shows the exact time you can pull, in your local time.
  NOT YET         - still a lot of time left. Also shows when you can pull.

"Copy result" copies the result text (e.g. for a ticket note).

Daylight saving time is handled automatically for the US, Bosnia and
Herzegovina and every other location in the list.


NOTES
-----
- City lookup for small US towns uses the internet (free Open-Meteo service).
  Without internet the app still works and uses the safest pull time.
- Settings are stored per user in %APPDATA%\PullCloud\settings.json
- Requires 64-bit Windows 10 or 11.
