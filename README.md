# AutoVMRDPService
This simple Windows service will listen for an RDP connection; and when one is requested, will start a specified Hyper-V VM
and forward the connection to it.  When the connection is closed, after a configurable delay, the VM will be automatically
shut down.

## Installation
This is a Topshelf-based service.  To install it, simply run `AutoVMRDPService.exe install`.
All [the usual Topshelf installation options](http://docs.topshelf-project.com/en/latest/overview/commandline.html) are
available.

## Uninstallation
To uninstall, run `AutoVMRDPService.exe uninstall`, then delete the executable and its configuration file manually.

## Configuration
All configuration is done via AppSettings in the AutoVMRDPService.exe.config file.  The settings are as follows:

| Key    | Description |
|--------|-------------|
| `vmrdp.ListenPort` | The port on localhost on the *host* that the service should listen to for new RDP connections. |
| `vmrdp.VMName` | The name of the Hyper-V VM to start when a connection is requested. |
| `vmrdp.VMHost` | The network hostname of the VM. |
| `vmrdp.VMPort` | The port number the VM will be listening on for RDP connection (usually 3389). |
| `vmrdp.ShutdownDelaySec` | The delay, in seconds, after disconnecting before the VM will be shut down.  If you reconnect before this delay elapses, you'll just be reconnected to the already-running VM. |
