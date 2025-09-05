; NSIS Unicode installer for Win11 Extra Clock
; Build-time defines expected (from GitHub Actions):
;  /DAPP_NAME="Win11 Extra Clock"
;  /DCOMPANY="Float Web"
;  /DAPP_VERSION="1.0.0"
;  /DPUBLISH_DIR="D:\a\...\out\publish"
;  /DEXE_NAME="Win11 Extra Clock.exe"
;  /DLICENSE_RTF="...\installer\assets\license.rtf"
;  /DOUTFILE="...\dist\Win11ExtraClock-x64-1.0.0-setup.exe"

!ifndef APP_NAME
  !define APP_NAME "Win11 Extra Clock"
!endif
!ifndef COMPANY
  !define COMPANY "Float Web"
!endif
!ifndef APP_VERSION
  !define APP_VERSION "1.0.0"
!endif
!ifndef PUBLISH_DIR
  !define PUBLISH_DIR "out\publish"
!endif
!ifndef EXE_NAME
  !define EXE_NAME "Win11 Extra Clock.exe"
!endif
!ifndef LICENSE_RTF
  !define LICENSE_RTF "installer\assets\license.rtf"
!endif
!ifndef OUTFILE
  !define OUTFILE "dist\Win11ExtraClock-x64-${APP_VERSION}-setup.exe"
!endif

Unicode true
RequestExecutionLevel admin
SetCompressor /SOLID lzma
Name "${APP_NAME}"
OutFile "${OUTFILE}"
InstallDir "$ProgramFiles64\${APP_NAME}"
InstallDirRegKey HKLM "Software\${COMPANY}\${APP_NAME}" "InstallDir"

; Version info
VIProductVersion "${APP_VERSION}.0"
VIAddVersionKey /LANG=1033 "ProductName"  "${APP_NAME}"
VIAddVersionKey /LANG=1033 "CompanyName"  "${COMPANY}"
VIAddVersionKey /LANG=1033 "FileDescription" "${APP_NAME} Setup"
VIAddVersionKey /LANG=1033 "FileVersion"  "${APP_VERSION}"

!include "MUI2.nsh"
!define MUI_ABORTWARNING

!insertmacro MUI_PAGE_LICENSE "${LICENSE_RTF}"
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!define MUI_FINISHPAGE_RUN "$InstDir\${EXE_NAME}"
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

!insertmacro MUI_LANGUAGE "English"
!insertmacro MUI_LANGUAGE "Romanian"

Section "Install" SEC01
  SetOutPath "$InstDir"
  
  File /r "${PUBLISH_DIR}\*.*"

  ; Start Menu
  CreateDirectory "$SMPROGRAMS\${APP_NAME}"
  CreateShortCut "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk" "$InstDir\${EXE_NAME}"

  ; Startup
  CreateShortCut "$CommonStartup\${APP_NAME}.lnk" "$InstDir\${EXE_NAME}"

  ; Uninstaller
  WriteUninstaller "$InstDir\Uninstall.exe"

  ; ARP + registry
  WriteRegStr HKLM "Software\${COMPANY}\${APP_NAME}" "InstallDir" "$InstDir"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "DisplayName" "${APP_NAME}"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "Publisher"   "${COMPANY}"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "DisplayVersion" "${APP_VERSION}"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "InstallLocation" "$InstDir"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "UninstallString" "$\"$InstDir\Uninstall.exe$\""
SectionEnd

Section "Uninstall"
  ; Shortcuturi
  Delete "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk"
  RMDir  "$SMPROGRAMS\${APP_NAME}"
  Delete "$CommonStartup\${APP_NAME}.lnk"

  ; Files
  RMDir /r "$InstDir"

  ; Registry
  DeleteRegKey HKLM "Software\${COMPANY}\${APP_NAME}"
  DeleteRegKey HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}"
SectionEnd
