namespace AudioThread.Domain.ValueObjects;

public readonly record struct AudioFileId(Guid Value)
{
    public static AudioFileId New() => new(Guid.NewGuid());

    public static AudioFileId From(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Audio file id cannot be empty.", nameof(value));
        }

        return new AudioFileId(value);
    }

    public override string ToString() => Value.ToString();
}
