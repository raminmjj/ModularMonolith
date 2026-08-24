using ModularMonolith.DDD.Common;

namespace ModularMonolith.DDD.Common;

public abstract class AggregateRoot<TId> : Entity<TId> where TId : struct { }
