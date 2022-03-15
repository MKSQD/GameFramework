namespace GameFramework {
    public enum MessageId {
        // To Server
        LoadSceneDone = Cube.Transport.MessageId.FirstUserId,

        // To Client
        LoadScene,

        PossessPawn,
        Commands,
        CommandsAccepted,

        FirstUserId
    }
}