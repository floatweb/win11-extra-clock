function Controller() {}

Controller.prototype.FinishedPageCallback = function() {
    if (!installer.isInstaller() || installer.status !== QInstaller.Success)
        return;

    var exe = installer.value("TargetDir") + "\\Win11 Extra Clock.exe";

    try {
        QProcess.startDetached(exe, []);
    } catch (e) {
        var url = "file:///" + exe.replace(/\\/g, "/");
        QDesktopServices.openUrl(url);
    }
}
