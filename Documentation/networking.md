# Networking plan

Local development must work without credentials or services. Introduce interfaces only when consumed: INetworkService, IPlayerSession, ILobbyService, IMatchService and IPlayerDataService. Local authority owns match rules; clients submit intent. Validate range, state, ownership, keys and objective actions at authority.

Fusion 2 integration follows local gameplay tests. Verify official APIs then, including tick simulation, prediction, reconciliation, spawn ownership, RPC validation and reconnect. Local tests are not evidence of network correctness. PlayFab comes after multiplayer tests, for account/lobby/profile integration. Neither SDK is installed. Never commit credentials.
