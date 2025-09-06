function Component() {}

Component.prototype.createOperations = function() {
    component.createOperations();

    var exe = "@TargetDir@\\Win11 Extra Clock.exe";

    component.addOperation(
        "CreateShortcut",
        exe,
        "@StartMenuDir@/Win11 Extra Clock.lnk",
        "workingDirectory=@TargetDir@",
        "iconId=0"
    );

    var startup = installer.environmentVariable("APPDATA")
        + "\\Microsoft\\Windows\\Start Menu\\Programs\\Startup\\Win11 Extra Clock.lnk";

    component.addOperation(
        "CreateShortcut",
        exe,
        startup,
        "workingDirectory=@TargetDir@",
        "iconId=0"
    );

}
