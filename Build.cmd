@ECHO OFF
SETLOCAL ENABLEDELAYEDEXPANSION

REM This script will build Open Rails. It needs a range of tools available in the PATH (they're checked below) and can produce 3 different kinds of build:
REM   - Unstable - doesn't include documentation or installers
REM   - Testing  - includes documentation but not installers
REM   - Stable   - includes documentation and installers

REM Check for necessary tools.
SET CheckToolInPath.Missing=0
CALL :check-tool-in-path "svn.exe" "Subversion command-line tool"
CALL :check-tool-in-path "MSBuild.exe" "Microsoft Visual Studio build tool"
CALL :check-tool-in-path "lazbuild.exe" "Lazarus compiler"
CALL :check-tool-in-path "strip.exe" "Lazarus command-line tool"
CALL :check-tool-in-path "xunit.console.x86.exe" "XUnit command-line tool"
CALL :check-tool-in-path "editbin.exe" "Microsoft Visual Studio editbin command-line tool"
CALL :check-tool-in-path "OfficeToPDF.exe" "Office-to-PDF conversion tool"
CALL :check-tool-in-path "7za.exe" "7-zip command-line tool"
CALL :check-tool-in-path "iscc.exe" "Inno Setup 5 compiler"
IF %CheckToolInPath.Missing% GTR 0 (
	TIMEOUT /T 10
)

REM Check for necessary directory.
IF NOT EXIST "Source\ORTS.sln" (
	>&2 ECHO ERROR: Unexpected current directory.
	ECHO Run "Build.cmd" in the parent directory of "ORTS.sln" ^(the directory "Build.cmd" is in^).
	EXIT /B 1
)

REM Get requested build mode.
SET Mode=-
IF "%~1" == "unstable" SET Mode=Unstable
IF "%~1" == "testing"  SET Mode=Testing
IF "%~1" == "stable"   SET Mode=Stable
IF "%Mode%" == "-" (
	>&2 ECHO ERROR: No build mode specified.
	ECHO Run "Build.cmd MODE" where MODE is "unstable", "testing" or "stable".
	EXIT /B 1
)

IF "%Mode%" == "Stable" (
	CALL :create "Microsoft .NET Framework Redistributable 3.5 SP1"
	CALL :create "Microsoft .NET Framework Redistributable 3.5 SP1 download manager"
	CALL :create "Microsoft XNA Framework Redistributable 3.1"
	IF NOT EXIST "Microsoft .NET Framework Redistributable 3.5 SP1\dotnetfx35.exe" (
		>&2 ECHO ERROR: Missing required file for "%Mode%" build: "Microsoft .NET Framework Redistributable 3.5 SP1\dotnetfx35.exe".
		EXIT /B 1
	)
	IF NOT EXIST "Microsoft .NET Framework Redistributable 3.5 SP1 download manager\dotnetfx35setup.exe" (
		>&2 ECHO ERROR: Missing required file for "%Mode%" build: "Microsoft .NET Framework Redistributable 3.5 SP1 download manager\dotnetfx35setup.exe".
		EXIT /B 1
	)
	IF NOT EXIST "Microsoft XNA Framework Redistributable 3.1\xnafx31_redist.msi" (
		>&2 ECHO ERROR: Missing required file for "%Mode%" build: "Microsoft XNA Framework Redistributable 3.1\xnafx31_redist.msi".
		EXIT /B 1
	)
)

REM Recreate Program directory for output.
CALL :recreate "Program" || GOTO :error

REM Build main program.
MSBuild Source\ORTS.sln /t:Clean;Build /p:Configuration=Release || GOTO :error

REM Build contributed Timetable Editor.
PUSHD Source\Contrib\TimetableEditor && CALL Build.cmd && POPD || GOTO :error

REM Get Subversion revision.
SET Revision=000
FOR /F "usebackq tokens=1" %%R IN (`svn --non-interactive info --show-item revision Source`) DO SET Revision=%%R
IF "%Revision%" == "000" (
	>&2 ECHO WARNING: No Subversion revision found.
)

REM Set update channel.
>>Program\Updater.ini ECHO Channel=string:%Mode% || GOTO :error
ECHO Set update channel to "%Mode%".

REM Set revision number.
>Program\Revision.txt ECHO $Revision: %Revision% $ || GOTO :error
ECHO Set revision number to "%Revision%".

REM Build locales.
PUSHD Source\Locales && CALL Update.bat non-interactive && POPD || GOTO :error

REM Run unit tests (9009 means XUnit itself wasn't found, which is an error).
xunit.console.x86 Program\Tests.dll /nunit xunit.xml
IF "%ERRORLEVEL%" == "9009" GOTO :error

CALL :copy "Program\RunActivity.exe" "Program\RunActivityLAA.exe" || GOTO :error
editbin /NOLOGO /LARGEADDRESSAWARE "Program\RunActivityLAA.exe" || GOTO :error
ECHO Created large address aware version of RunActivity.exe.

IF NOT "%Mode%" == "Unstable" (
	REM Restart the Office Click2Run service as this frequently breaks builds.
	NET stop ClickToRunSvc
	NET start ClickToRunSvc

	REM Recreate Documentation folder for output.
	CALL :recreate "Program\Documentation" || GOTO :error

	REM Compile the documentation.
	FOR /R "Source\Documentation" %%F IN (*.doc *.docx *.docm *.xls *.xlsx *.xlsm *.odt) DO ECHO %%~F && OfficeToPDF.exe /bookmarks /print "%%~F" "Program\Documentation\%%~nF.pdf" || GOTO :error
	PUSHD "Source\Documentation\Manual" && CALL make.bat clean && POPD || GOTO :error
	PUSHD "Source\Documentation\Manual" && CALL make.bat latexpdf && POPD || GOTO :error

	REM Copy the documentation.
	FOR /R "Source\Documentation" %%F IN (*.pdf *.txt) DO CALL :copy "%%~F" "Program\Documentation\%%~nF.pdf" || GOTO :error
	ROBOCOPY /MIR /NJH /NJS "Source\Documentation\SampleFiles" "Program\Documentation\SampleFiles"
	IF %ERRORLEVEL% GEQ 8 GOTO :error

	REM Copy the manual separately.
	CALL :copy "Program\Documentation\Manual.pdf" "OpenRails-%Mode%-Manual.pdf" || GOTO :error
)

IF "%Mode%" == "Stable" (
	ROBOCOPY /MIR /NJH /NJS "Program" "Open Rails\Program" /XD Documentation
	IF %ERRORLEVEL% GEQ 8 GOTO :error
	ROBOCOPY /MIR /NJH /NJS "Program\Documentation" "Open Rails\Documentation"
	IF %ERRORLEVEL% GEQ 8 GOTO :error
	>"Source\Installer\OpenRails shared\Version.iss" ECHO #define MyAppVersion "%Version%.%Revision%" || GOTO :error
	iscc "Source\Installer\OpenRails from download\OpenRails from download.iss" || GOTO :error
	iscc "Source\Installer\OpenRails from DVD\OpenRails from DVD.iss" || GOTO :error
	CALL :move "Source\Installer\OpenRails from download\Output\OpenRailsTestingSetup.exe" "OpenRails-%Mode%-Setup.exe" || GOTO :error
	CALL :move "Source\Installer\OpenRails from DVD\Output\OpenRailsTestingDVDSetup.exe" "OpenRails-%Mode%-DVDSetup.exe" || GOTO :error
)

REM *** Special build step: signs binaries ***
REM IF NOT "%JENKINS_TOOLS%" == "" (
REM 	%JENKINS_TOOLS%\build.cmd || GOTO :error
REM )

REM Create binary and source zips.
CALL :delete "OpenRails-%Mode%*.zip" || GOTO :error
PUSHD "Program" && 7za.exe a -r -tzip "..\OpenRails-%Mode%.zip" . && POPD || GOTO :error
7za.exe a -r -tzip -x^^!.* -x^^!obj -x^^!lib -x^^!_build -x^^!*.bak "OpenRails-%Mode%-Source.zip" "Source" || GOTO :error

ENDLOCAL
GOTO :EOF

REM Checks for a tool (%1) exists in %PATH% and reports a warning otherwise (%2 is descriptive name for tool).
:check-tool-in-path
REM ECHO  1:%1:
REM ECHO ~1:%~1:
REM ECHO $1:%~$PATH:1:
IF "%~$PATH:1" == "" (
	IF NOT "%~2" == "" (
		>&2 ECHO WARNING: "%~1" ^(%~2^) cannot be found in %%PATH%%. Build may fail.
	) ELSE (
		>&2 ECHO WARNING: "%~1" cannot be found in %%PATH%%. Build may fail.
	)
	SET /A CheckToolInPath.Missing=CheckToolInPath.Missing+1
)
GOTO :EOF

REM Utility for creating a directories with logging.
:create
ECHO Create "%~1"
IF NOT EXIST "%~1" MKDIR "%~1"
GOTO :EOF

REM Utility for recreating a directories with logging.
:recreate
ECHO Recreate "%~1"
(IF EXIST "%~1" RMDIR "%~1" /S /Q) && MKDIR "%~1"
GOTO :EOF

REM Utility for moving files with logging.
:move
ECHO Move "%~1" "%~2"
1>nul MOVE /Y "%~1" "%~2"
GOTO :EOF

REM Utility for copying files with logging.
:copy
ECHO Copy "%~1" "%~2"
1>nul COPY /Y "%~1" "%~2"
GOTO :EOF

:delete
ECHO Delete "%~1"
DEL /F /Q "%~1"
GOTO :EOF

REM Reports that an error occurred.
:error
>&2 ECHO ERROR: Failure during build ^(check the output above^). Error %ERRORLEVEL%.
EXIT /B 1
