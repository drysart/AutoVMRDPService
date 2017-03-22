using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace AutoVMRDPService
{
    class VMRDPService
    {
        private TcpListener m_listener;

        public void Start()
        {
            Task.Run(ServiceMain);
        }

        public void Stop()
        {
            m_listener.Stop();
        }

        private async Task ServiceMain()
        {
            int listenPort = Convert.ToInt32(System.Configuration.ConfigurationManager.AppSettings["vmrdp.ListenPort"] ?? "1313");
            string vmName = System.Configuration.ConfigurationManager.AppSettings["vmrdp.VMName"] ?? "Support VM";
            string vmHost = System.Configuration.ConfigurationManager.AppSettings["vmrdp.VMHost"] ?? "SupportVM";
            int vmPort = Convert.ToInt32(System.Configuration.ConfigurationManager.AppSettings["vmrdp.VMPort"] ?? "3389");
            int shutdownDelaySec = Convert.ToInt32(System.Configuration.ConfigurationManager.AppSettings["vmrdp.ShutdownDelaySec"] ?? "240");

            Task shutdownTask = null;
            try
            {
                m_listener = new TcpListener(new System.Net.IPEndPoint(IPAddress.Loopback, listenPort));
                m_listener.Start();
                while (true)
                {
                    var atcliTask = m_listener.AcceptTcpClientAsync();
                    TrimWorkingSet();
                    var client = await atcliTask;
                    using (var cns = client.GetStream())
                    {
                        shutdownTask = null;

                        var ms = new ManagementScope("root\\virtualization\\v2");
                        ms.Connect();

                        var mc = new ManagementClass(ms, new ManagementPath("Msvm_ComputerSystem"), null)
                            .GetInstances()
                            .OfType<ManagementObject>()
                            .Where(x => "Virtual Machine" == (string)x["Caption"])
                            .Where(x => (x["ElementName"] as string) == vmName)
                            .First();

                        StartVM(ms, mc);

                        TcpClient sconn = null;
                        while (sconn == null)
                        {
                            try
                            {
                                sconn = new TcpClient(vmHost, vmPort);
                            }
                            catch (Exception)
                            {
                                await Task.Delay(500);
                            }
                        }

                        using (var sns = sconn.GetStream())
                        {
                            byte[] s2cbuf = new byte[4096];
                            byte[] c2sbuf = new byte[4096];

                            var nullTask = new TaskCompletionSource<int>().Task;

                            var creadTask = cns.ReadAsync(c2sbuf, 0, c2sbuf.Length);
                            var sreadTask = sns.ReadAsync(s2cbuf, 0, s2cbuf.Length);
                            var cwriteTask = (Task)nullTask;
                            var swriteTask = (Task)nullTask;

                            try
                            {
                                TrimWorkingSet();
                                while (true)
                                {
                                    var readyTask = Task.WaitAny(creadTask, sreadTask, swriteTask, cwriteTask);
                                    if (readyTask == 0)
                                    {
                                        // client read done
                                        var bytesRead = creadTask.Result;
                                        creadTask = nullTask;

                                        swriteTask = sns.WriteAsync(c2sbuf, 0, bytesRead);
                                    }
                                    else if (readyTask == 1)
                                    {
                                        // server read done
                                        var bytesRead = sreadTask.Result;
                                        sreadTask = nullTask;

                                        cwriteTask = cns.WriteAsync(s2cbuf, 0, bytesRead);
                                    }
                                    else if (readyTask == 2)
                                    {
                                        // server write done
                                        if (swriteTask.IsFaulted)
                                            throw swriteTask.Exception;

                                        swriteTask = (Task)nullTask;
                                        creadTask = cns.ReadAsync(c2sbuf, 0, c2sbuf.Length);
                                    }
                                    else if (readyTask == 3)
                                    {
                                        // client write done
                                        if (cwriteTask.IsFaulted)
                                            throw cwriteTask.Exception;

                                        cwriteTask = (Task)nullTask;
                                        sreadTask = sns.ReadAsync(s2cbuf, 0, s2cbuf.Length);
                                    }
                                }
                            }
                            catch (Exception)
                            {
                                var myTask = Task.Delay(shutdownDelaySec * 1000);
                                shutdownTask = myTask;
                                var ignored = myTask.ContinueWith((txx) =>
                                {
                                    if (shutdownTask == myTask)
                                    {
                                        ShutdownVM(ms, mc);
                                        TrimWorkingSet();
                                    }
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {

            }
        }

        [DllImport("psapi")]
        public static extern bool EmptyWorkingSet(IntPtr hProcess);

        private void TrimWorkingSet()
        {
            var ph = System.Diagnostics.Process.GetCurrentProcess().Handle;
            EmptyWorkingSet(ph);
        }

        private uint? StartVM(ManagementScope scope, ManagementObject mc)
        {
            var msvc = new ManagementClass(scope, new ManagementPath("Msvm_VirtualSystemManagementService"), null)
                .GetInstances()
                .OfType<ManagementObject>()
                .First();

            var inParams = msvc.GetMethodParameters("RequestStateChange");
            inParams["RequestedState"] = 2;  // Running
            var outParams = mc.InvokeMethod("RequestStateChange", inParams, null);

            if (outParams != null)
                return (uint)outParams["ReturnValue"];
            else
                return null;
        }

        private uint? ShutdownVM(ManagementScope scope, ManagementObject mc)
        {
            var vmName = mc["Name"] as string;

            var msvc = new ManagementClass(scope, new ManagementPath("Msvm_ShutdownComponent"), null)
                .GetInstances()
                .OfType<ManagementObject>()
                .Where(x => (x["SystemName"] as string) == vmName)
                .First();

            var inParams = msvc.GetMethodParameters("InitiateShutdown");
            inParams["Force"] = true;
            inParams["Reason"] = "Remote disconnected";
            var outParams = msvc.InvokeMethod("InitiateShutdown", inParams, null);

            if (outParams != null)
                return (uint)outParams["ReturnValue"];
            else
                return null;
        }
    }
}
