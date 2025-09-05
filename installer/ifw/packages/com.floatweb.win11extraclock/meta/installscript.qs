function Component() {}

Component.prototype.createOperations = function() {
    component.createOperations();

    var exe = "@TargetDir@\\Win11 Extra Clock.exe";

    component.addOperation("CreateShortcut",
        exe,
        "@StartMenuDir@/Win11 Extra Clock.lnk",
        "WORKINGDIR=@TargetDir@",
        "ICONID=0");

    var startup = installer.environmentVariable("APPDATA")
                + "\\Microsoft\\Windows\\Start Menu\\Programs\\Startup\\Win11 Extra Clock.lnk";

    component.addOperation("CreateShortcut",
        exe,
        startup,
        "WORKINGDIR=@TargetDir@",
        "ICONID=0");

    component.addOperation("Delete", startup);
}
