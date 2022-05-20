namespace GameFramework {
    public enum MessageId {
        // Server -> Client
        PossessPawn = Cube.Transport.MessageId.FirstUserId,
        Commands,
        CommandsAccepted,

        FirstUserId
    }
}