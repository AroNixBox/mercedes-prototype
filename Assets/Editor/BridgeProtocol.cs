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
}
