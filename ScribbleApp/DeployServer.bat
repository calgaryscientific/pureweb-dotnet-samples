@REM should typically be invoked from post build event as
@REM "$(ProjectDir)\DeployServer.bat" "$(TargetName)" "$(ProjectDir)" "$(TargetDir)"

if exist "%PUREWEB_HOME%\apps\%1" goto deploy
echo "Creating directory %PUREWEB_HOME%\apps\%1..." 
md "%PUREWEB_HOME%\apps\%1" 

:deploy
echo "Sending application files to  %PUREWEB_HOME%\apps\%1..." 
xcopy /YFDI %3\* "%PUREWEB_HOME%\apps\%1"

@REM copy x264 libs to $(TargetDir) to enable debugging x264 encoding
xcopy /YFDI "%PUREWEB_LIBS%\C++\VS2010\lib\libx264*.dll" "%PUREWEB_HOME%\apps\%1"

SET targetDir=###%2%###
SET targetDir=%targetDir:"###=%
SET targetDir=%targetDir:###"=%
SET targetDir=%targetDir:###=%

if exist %PUREWEB_HOME%\services\service-manager-cfg.exe (
	%PUREWEB_HOME%\services\service-manager-cfg.exe -configFile %PUREWEB_HOME%\services\service_config.json -action add -changeFile %targetDir%\service.json 
)