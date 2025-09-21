; NSIS Installer Script for Modrix
; Modern UI with custom design and beautiful interface

;--------------------------------
; Include Modern UI
!include "MUI2.nsh"
!include "nsDialogs.nsh"
!include "LogicLib.nsh"

;--------------------------------
; General Settings

; Name and file
Name "Modrix"
OutFile "nsis-installer\ModrixSetup-NSIS.exe"

; Default installation folder
InstallDir "$PROGRAMFILES64\Modrix"

; Get installation folder from registry if available
InstallDirRegKey HKCU "Software\Modrix" ""

; Request application privileges for Windows Vista/7/8/10/11
RequestExecutionLevel admin

; Compression
SetCompressor /SOLID lzma

; Version Information
VIProductVersion "1.0.0.0"
VIAddVersionKey "ProductName" "Modrix"
VIAddVersionKey "FileDescription" "Modrix Minecraft Mod Development IDE"
VIAddVersionKey "LegalCopyright" "© 2024 Modrix Development Team"
VIAddVersionKey "FileVersion" "1.0.0"
VIAddVersionKey "ProductVersion" "1.0.0"
VIAddVersionKey "CompanyName" "Modrix Development Team"

;--------------------------------
; Interface Settings

; Use custom colors and styling
!define MUI_ABORTWARNING
!define MUI_ICON "Resources\ModrixIcon.ico"
!define MUI_UNICON "Resources\ModrixIcon.ico"

; Custom header image (modern look)
!define MUI_HEADERIMAGE
!define MUI_HEADERIMAGE_RIGHT
!define MUI_HEADERIMAGE_BITMAP "Resources\ModrixIcon.ico"
!define MUI_HEADERIMAGE_UNBITMAP "Resources\ModrixIcon.ico"

; Welcome and finish page customizations
!define MUI_WELCOMEFINISHPAGE_BITMAP "Resources\ModrixIcon.ico"
!define MUI_UNWELCOMEFINISHPAGE_BITMAP "Resources\ModrixIcon.ico"

; Custom colors - Modern blue theme
!define MUI_BGCOLOR "0x1e1e1e"
!define MUI_TEXTCOLOR "0xffffff"

; Welcome page settings
!define MUI_WELCOMEPAGE_TITLE "Welcome to Modrix Setup"
!define MUI_WELCOMEPAGE_TEXT "This wizard will guide you through the installation of Modrix.$\r$\n$\r$\nModrix is a powerful Minecraft mod development IDE that simplifies the creation of mods for Fabric and Forge.$\r$\n$\r$\nClick Next to continue."

; Finish page settings
!define MUI_FINISHPAGE_TITLE "Modrix Installation Complete"
!define MUI_FINISHPAGE_TEXT "Modrix has been successfully installed on your computer.$\r$\n$\r$\nClick Finish to close this wizard."
!define MUI_FINISHPAGE_RUN "$INSTDIR\Modrix.exe"
!define MUI_FINISHPAGE_RUN_TEXT "Launch Modrix now"
!define MUI_FINISHPAGE_LINK "Visit the Modrix website"
!define MUI_FINISHPAGE_LINK_LOCATION "https://github.com/Shlomo1412/Modrix"

; Components page settings
!define MUI_COMPONENTSPAGE_SMALLDESC

;--------------------------------
; Pages

; Welcome page
!insertmacro MUI_PAGE_WELCOME

; License page
!insertmacro MUI_PAGE_LICENSE "LICENSE.txt"

; Components page
!insertmacro MUI_PAGE_COMPONENTS

; Directory page
!insertmacro MUI_PAGE_DIRECTORY

; Custom information page
Page custom CustomInfoPage

; Installation page with progress
!insertmacro MUI_PAGE_INSTFILES

; Finish page
!insertmacro MUI_PAGE_FINISH

; Uninstaller pages
!insertmacro MUI_UNPAGE_WELCOME
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_UNPAGE_FINISH

;--------------------------------
; Languages

!insertmacro MUI_LANGUAGE "English"

;--------------------------------
; Custom Information Page Function

Function CustomInfoPage
    nsDialogs::Create 1018
    Pop $0
    
    ; Page title
    ${NSD_CreateLabel} 0 10u 100% 20u "Modrix - Minecraft Mod Development IDE"
    Pop $1
    CreateFont $2 "Segoe UI" 16 700
    SendMessage $1 ${WM_SETFONT} $2 0
    
    ; Description
    ${NSD_CreateLabel} 0 40u 100% 60u "Modrix is a comprehensive development environment for creating Minecraft mods. It provides:$\r$\n$\r$\n• Visual mod element editor$\r$\n• Built-in code templates for Fabric and Forge$\r$\n• Integrated build and testing tools$\r$\n• Resource management system$\r$\n• Modern WPF-based interface"
    Pop $3
    
    ; Features box
    ${NSD_CreateGroupBox} 0 110u 100% 60u "Key Features"
    Pop $4
    
    ${NSD_CreateLabel} 10u 130u 90% 40u "✓ Support for Fabric and Forge mod loaders$\r$\n✓ Drag-and-drop mod element creation$\r$\n✓ Real-time code compilation and testing$\r$\n✓ Built-in resource editor and texture manager"
    Pop $5
    
    nsDialogs::Show
FunctionEnd

;--------------------------------
; Installation Components

; Main application (required)
Section "Modrix Application" SecMain
    SectionIn RO  ; Read-only, always installed
    
    ; Set output path to the installation directory
    SetOutPath $INSTDIR
    
    ; Application files
    File /r "exe\*.*"
    
    ; Store installation folder
    WriteRegStr HKCU "Software\Modrix" "" $INSTDIR
    
    ; Create uninstaller
    WriteUninstaller "$INSTDIR\Uninstall.exe"
    
    ; Add to Add/Remove Programs
    WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\Modrix" "DisplayName" "Modrix"
    WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\Modrix" "UninstallString" "$INSTDIR\Uninstall.exe"
    WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\Modrix" "InstallLocation" "$INSTDIR"
    WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\Modrix" "DisplayIcon" "$INSTDIR\Modrix.exe"
    WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\Modrix" "Publisher" "Modrix Development Team"
    WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\Modrix" "DisplayVersion" "1.0.0"
    WriteRegDWORD HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\Modrix" "NoModify" 1
    WriteRegDWORD HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\Modrix" "NoRepair" 1
    
    ; File associations for .modrix files
    WriteRegStr HKCR ".modrix" "" "ModrixProject"
    WriteRegStr HKCR "ModrixProject" "" "Modrix Project File"
    WriteRegStr HKCR "ModrixProject\DefaultIcon" "" "$INSTDIR\Modrix.exe,0"
    WriteRegStr HKCR "ModrixProject\shell\open\command" "" '"$INSTDIR\Modrix.exe" "%1"'
    
SectionEnd

; Desktop shortcut
Section "Desktop Shortcut" SecDesktop
    CreateShortcut "$DESKTOP\Modrix.exe.lnk" "$INSTDIR\Modrix.exe" "" "$INSTDIR\Modrix.exe" 0
SectionEnd

; Start Menu shortcuts
Section "Start Menu Shortcuts" SecStartMenu
    CreateDirectory "$SMPROGRAMS\Modrix"
    CreateShortcut "$SMPROGRAMS\Modrix\Modrix.lnk" "$INSTDIR\Modrix.exe" "" "$INSTDIR\Modrix.exe" 0
    CreateShortcut "$SMPROGRAMS\Modrix\Uninstall Modrix.lnk" "$INSTDIR\Uninstall.exe" "" "$INSTDIR\Uninstall.exe" 0
SectionEnd

; Quick Launch shortcut (for older Windows versions)
Section /o "Quick Launch Shortcut" SecQuickLaunch
    CreateShortcut "$QUICKLAUNCH\Modrix.lnk" "$INSTDIR\Modrix.exe" "" "$INSTDIR\Modrix.exe" 0
SectionEnd

;--------------------------------
; Component Descriptions

LangString DESC_SecMain ${LANG_ENGLISH} "The core Modrix application and required files."
LangString DESC_SecDesktop ${LANG_ENGLISH} "Create a shortcut on the desktop for easy access."
LangString DESC_SecStartMenu ${LANG_ENGLISH} "Add Modrix to the Start Menu."
LangString DESC_SecQuickLaunch ${LANG_ENGLISH} "Add a Quick Launch shortcut (for older Windows versions)."

!insertmacro MUI_FUNCTION_DESCRIPTION_BEGIN
  !insertmacro MUI_DESCRIPTION_TEXT ${SecMain} $(DESC_SecMain)
  !insertmacro MUI_DESCRIPTION_TEXT ${SecDesktop} $(DESC_SecDesktop)
  !insertmacro MUI_DESCRIPTION_TEXT ${SecStartMenu} $(DESC_SecStartMenu)
  !insertmacro MUI_DESCRIPTION_TEXT ${SecQuickLaunch} $(DESC_SecQuickLaunch)
!insertmacro MUI_FUNCTION_DESCRIPTION_END

;--------------------------------
; Installation Events

Function .onInit
    ; Check if already installed
    ReadRegStr $R0 HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\Modrix" "UninstallString"
    StrCmp $R0 "" done
    
    MessageBox MB_OKCANCEL|MB_ICONEXCLAMATION \
        "Modrix is already installed. $\n$\nClick 'OK' to remove the previous version or 'Cancel' to cancel this upgrade." \
        IDOK uninst
    Abort
    
uninst:
    ClearErrors
    ExecWait '$R0 _?=$INSTDIR'
    
    IfErrors no_remove_uninstaller done
    no_remove_uninstaller:
    
done:
FunctionEnd

Function .onInstSuccess
    ; Launch application option
    MessageBox MB_YESNO "Installation completed successfully! Would you like to launch Modrix now?" IDNO NoLaunch
        Exec "$INSTDIR\Modrix.exe"
    NoLaunch:
FunctionEnd

;--------------------------------
; Uninstaller

Section "Uninstall"
    ; Remove files and uninstaller
    Delete "$INSTDIR\Uninstall.exe"
    RMDir /r "$INSTDIR"
    
    ; Remove shortcuts
    Delete "$DESKTOP\Modrix.exe.lnk"
    Delete "$SMPROGRAMS\Modrix\*.*"
    RMDir "$SMPROGRAMS\Modrix"
    Delete "$QUICKLAUNCH\Modrix.lnk"
    
    ; Remove registry keys
    DeleteRegKey HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\Modrix"
    DeleteRegKey HKCU "Software\Modrix"
    DeleteRegKey HKCR ".modrix"
    DeleteRegKey HKCR "ModrixProject"
    
    ; Remove installation directory if empty
    RMDir "$INSTDIR"
SectionEnd

Function un.onInit
    MessageBox MB_ICONQUESTION|MB_YESNO|MB_DEFBUTTON2 "Are you sure you want to completely remove Modrix and all of its components?" IDYES +2
    Abort
FunctionEnd

Function un.onUninstSuccess
    HideWindow
    MessageBox MB_ICONINFORMATION|MB_OK "Modrix was successfully removed from your computer."
FunctionEnd