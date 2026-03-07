[Setup]
AppName=SpectrumNotes
AppVersion=1.0.0
AppPublisher=Erik Martin
AppPublisherURL=https://erikmartin.com
WizardStyle=modern
DefaultDirName={autopf}\SpectrumNotes
DefaultGroupName=SpectrumNotes
UninstallDisplayIcon={app}\Spectrum.exe
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=output
OutputBaseFilename=SpectrumNotesSetup
SetupIconFile=E:\Projects\Spectrum\SpectrumNotes\Contents\favicon.ico
LicenseFile=E:\Projects\Spectrum\SpectrumNotes\LICENSE
MinVersion=10.0 

[Files]
Source: "E:\Projects\Spectrum\SpectrumNotes\bin\Release\net8.0-windows\win-x64\*"; \
  DestDir: "{app}"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{group}\SpectrumNotes"; Filename: "{app}\Spectrum.exe"
Name: "{group}\Uninstall SpectrumNotes"; Filename: "{uninstallexe}"
Name: "{autodesktop}\SpectrumNotes"; Filename: "{app}\Spectrum.exe"; \
  Tasks: desktopicon

[Tasks]
Name: desktopicon; Description: "Create a &desktop shortcut"; GroupDescription: "Additional icons:"

[Run]
; Optionally launch the app after install
Filename: "{app}\Spectrum.exe"; Description: "Launch SpectrumNotes"; \
  Flags: nowait postinstall skipifsilent
