public static class BridgeProtocol
{
    public const string SUCCESS = "[[PROMPTRETURN]] SUCCESS";

    public static string Failure(string reason) =>
        $"[[PROMPTRETURN]] FAILURE: {reason}\n" +
        "⛔ AGENT: Stop all tool calls. Show the reason above to the user exactly as written. " +
        "Then propose 2-3 concrete solution options the user can choose from (based on the failure reason). " +
        "Present them as numbered options. Do NOT execute any option yourself. Wait for the user to pick.";

    public static string AwaitingInput(string question, string option1, string option2) =>
        $"[[PROMPTRETURN]] AWAITING_INPUT: {question}\n" +
        $"OPTION_1: {option1}\n" +
        $"OPTION_2: {option2}\n" +
        "🔵 AGENT: Stop all tool calls. Show the question and both options above to the user. " +
        "Do NOT choose an option yourself. Wait for the user to pick, then call the same method again with the chosen parameter.";

    public static string Success(string details) => $"{SUCCESS}\n{details}";

    /// <summary>Stateless variant: the agent counts its own attempts (the skill caps them).</summary>
    public static string ValidationFailed(string report) =>
        $"[[PROMPTRETURN]] VALIDATION_FAILED:\n{report}\n" +
        "🟠 AGENT: The spline was built but is not valid. You MAY fix it yourself: move, add or remove intermediate points near the reported " +
        "t ranges (t 0 = start, 1 = end; 'nearest point' names the point to change) and call CarCam.BuildPath again with the same shotName. " +
        "Do NOT change start/end without asking the user if the user specified them. After 3 failed attempts for the same request, " +
        "stop, show the report to the user and propose 2-3 numbered options. Wait for the user to pick.";

    public static string ValidationFailed(string report, int attempt, int maxAttempts)
    {
        var header = $"[[PROMPTRETURN]] VALIDATION_FAILED (attempt {attempt}/{maxAttempts}):\n{report}\n";
        if (attempt < maxAttempts)
        {
            return header +
                   "🟠 AGENT: You MAY fix this yourself. Move, add or remove intermediate points near the reported t ranges " +
                   "(t 0 = start, 1 = end; 'nearest point' names the point to change) and call CarCam.BuildPath again. " +
                   "Do NOT change the endpoints without asking the user.";
        }
        return header +
               "🔵 AGENT: Stop all tool calls. The maximum number of automatic fix attempts is reached. " +
               "Show the report to the user and propose 2-3 numbered options (e.g. adjust intermediate points, change endpoints, change the look target). " +
               "Do NOT execute any option yourself. Wait for the user to pick.";
    }
}
