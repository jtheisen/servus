using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

static class WindowsJobs
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    public static extern SafeJobHandle CreateJobObject(
        IntPtr lpJobAttributes,
        string lpName
    );

    [DllImport("kernel32.dll")]
    public static extern bool SetInformationJobObject(
        IntPtr hJob,
        JobObjectInfoClass JobObjectExtendedLimitInformation,
        IntPtr lpJobObjectInfo,
        uint cbJobObjectInfoLength
    );

    [DllImport("kernel32.dll")]
    public static extern bool AssignProcessToJobObject(
        IntPtr hJob,
        IntPtr hProcess
    );

    [DllImport("kernel32.dll")]
    public static extern bool IsProcessInJob(
        IntPtr hProcess,
        IntPtr hJob,
        out bool result
    );

    [DllImport("kernel32.dll")]
    public static extern bool CloseHandle(IntPtr hObject);

    public class SafeJobHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public SafeJobHandle() : base(ownsHandle: true) { }

        protected override bool ReleaseHandle() => CloseHandle(handle);
    }

    public enum JobObjectInfoClass
    {
        JobObjectExtendedLimitInformation = 9
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
    {
        public Int64 PerProcessUserTimeLimit;
        public Int64 PerJobUserTimeLimit;
        public uint LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public Int64 Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IO_COUNTERS
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
    {
        public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
        public IO_COUNTERS IoInfo;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryUsed;
        public UIntPtr PeakJobMemoryUsed;
    }

    private const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x00002000;

    public static void ConfigureJobToKillOnClose(SafeJobHandle job)
    {
        var info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION();
        info.BasicLimitInformation.LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;

        var ptr = Marshal.AllocHGlobal(Marshal.SizeOf<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>());
        Marshal.StructureToPtr(info, ptr, false);

        try
        {
            if (!SetInformationJobObject(
                    job.DangerousGetHandle(),
                    JobObjectInfoClass.JobObjectExtendedLimitInformation,
                    ptr,
                    (uint)Marshal.SizeOf<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>()
                ))
            {
                throw new System.ComponentModel.Win32Exception();
            }
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }

    public static void AssignProcessToJob(SafeJobHandle job, Process process)
    {
        if (!AssignProcessToJobObject(job.DangerousGetHandle(), process.Handle))
            throw new System.ComponentModel.Win32Exception();
    }

    public static bool IsProcessInJob(SafeJobHandle job, Process process)
    {
        if (!IsProcessInJob(process.Handle, job.DangerousGetHandle(), out bool result))
            throw new System.ComponentModel.Win32Exception();

        return result;
    }
}