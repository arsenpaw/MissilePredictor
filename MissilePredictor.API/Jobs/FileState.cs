using System.Text.Json;

namespace MissilePredictor.Jobs;

public class FileState<T> where T : new()
{
    private readonly string _path;
    private T _value;

    public FileState(string path)
    {
        _path = path;

        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir); 

        _value = Load();
    }

    public T Value => _value;

    public void Save(Action<T> update)
    {
        update(_value);
        File.WriteAllText(_path, JsonSerializer.Serialize(_value,
            new JsonSerializerOptions { WriteIndented = true }));
    }

    private T Load()
    {
        if (!File.Exists(_path))
            return new T();

        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(_path)) ?? new T();
        }
        catch
        {
            return new T();
        }
    }
}