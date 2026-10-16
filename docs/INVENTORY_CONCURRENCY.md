# Inventory consistency

Every application mutation enters `LedgerGate`: a SERIALIZABLE transaction acquires SQL Server `sp_getapplock`, Exclusive, Transaction owner, resource `StockPilot.Ledger.v1`, 10-second timeout. All app instances connected to the same database contend on the same resource. Fresh balances and roles are read only after acquiring the lock; EF tracking is cleared. Product rowversion, nonnegative-stock / movement arithmetic SQL constraints, unique document numbers and unique `(ActorId, Key)` operation records add defenses.

This intentionally serializes writes for a small single-business deployment. It favors correctness over write throughput. Never bypass services with direct stock SQL or another app that ignores the gate. Read-only lists may observe ordinary committed data. Reports needing a multi-query coherent snapshot use the same gate.

Critical operations store a SHA-256 fingerprint of the command, actor/key and result in the transaction. Exact retry returns the original result; changed payload/key reuse returns 409. Clients retain the original key until outcome is known. Duplicate document receive/confirm under a new key is rejected by state. No automatic retry of individual statements; deadlock/timeout/concurrency/unique conflicts return 409 and the entire transaction rolls back. The client may retry the **whole original command with original key**, never just a stock update. This also handles unknown commit outcomes.

SQLite is supported only in the explicit Testing environment with a process-wide semaphore and relational constraints. It is workflow evidence, NOT proof of SQL Server locking. Native SQL tests must run against a newly generated `_test` database. No production or existing database is reset by tests.
