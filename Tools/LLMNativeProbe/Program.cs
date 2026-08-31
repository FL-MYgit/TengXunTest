using System.Runtime.InteropServices;

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: LLMNativeProbe <backend-dll> <model.gguf> [context] [batch]");
    return 2;
}

string libraryPath = Path.GetFullPath(args[0]);
string modelPath = Path.GetFullPath(args[1]);
int contextSize = args.Length > 2 ? int.Parse(args[2]) : 2048;
int batchSize = args.Length > 3 ? int.Parse(args[3]) : 128;

Console.WriteLine($"Library: {libraryPath}");
Console.WriteLine($"Model:   {modelPath}");
Console.WriteLine($"Context: {contextSize}, Batch: {batchSize}");

IntPtr library = NativeLibrary.Load(libraryPath);
LogCallback logCallback = message =>
{
    if (!string.IsNullOrWhiteSpace(message)) Console.WriteLine($"[native] {message}");
};

GetDelegate<DebugDelegate>(library, "LLM_Debug")(5);
GetDelegate<LoggingCallbackDelegate>(library, "LLM_Logging_Callback")(logCallback);

IntPtr service = GetDelegate<ConstructDelegate>(library, "LLMService_Construct")(
    modelPath, 1, -1, 0, false, contextSize, batchSize, false, 0, IntPtr.Zero);

int statusCode = GetDelegate<StatusCodeDelegate>(library, "LLM_Status_Code")();
IntPtr statusPointer = GetDelegate<StatusMessageDelegate>(library, "LLM_Status_Message")();
string statusMessage = Marshal.PtrToStringAnsi(statusPointer) ?? "";
Console.WriteLine($"Status: {statusCode}: {statusMessage}");

if (service == IntPtr.Zero)
{
    Console.Error.WriteLine("RESULT: FAILED (null service)");
    return 1;
}

Console.WriteLine("RESULT: CONSTRUCTION SUCCEEDED");
GetDelegate<DeleteDelegate>(library, "LLM_Delete")(service);
GC.KeepAlive(logCallback);
return 0;

static T GetDelegate<T>(IntPtr library, string name) where T : Delegate =>
    Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(library, name));

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
delegate void DebugDelegate(int level);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
delegate void LogCallback([MarshalAs(UnmanagedType.LPStr)] string message);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
delegate void LoggingCallbackDelegate(LogCallback callback);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
delegate IntPtr ConstructDelegate(
    [MarshalAs(UnmanagedType.LPStr)] string modelPath,
    int numSlots,
    int numThreads,
    int numGpuLayers,
    [MarshalAs(UnmanagedType.I1)] bool flashAttention,
    int contextSize,
    int batchSize,
    [MarshalAs(UnmanagedType.I1)] bool embeddingOnly,
    int loraCount,
    IntPtr loraPaths);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
delegate int StatusCodeDelegate();

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
delegate IntPtr StatusMessageDelegate();

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
delegate void DeleteDelegate(IntPtr service);
