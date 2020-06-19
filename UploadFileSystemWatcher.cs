using System;
using System.IO;
using System.Threading;

namespace AzCp
{
  class UploadFileSystemWatcher : IDisposable
  {
    private long _changesInUploadFolder;
    private bool disposedValue;
    private readonly FileSystemWatcher _watcher;

    public bool AnyChangesInUploadFolder
    {
      get
      {
        return Interlocked.Read(ref _changesInUploadFolder) != 0;
      }
    }

    public void ResetChangeCount()
    {
      Interlocked.Exchange(ref _changesInUploadFolder, 0);
    }


    public UploadFileSystemWatcher(string folder)
    {
      _watcher = CreateFileSystemWatcher(folder);
    }

    private FileSystemWatcher CreateFileSystemWatcher(string folder)
    {
      var watcher = new FileSystemWatcher
      {
        Path = folder,

        // Watch for changes in LastWrite times, and the renaming of files or directories.
        NotifyFilter = // NotifyFilters.LastAccess
                           NotifyFilters.LastWrite
                           | NotifyFilters.FileName
                           | NotifyFilters.DirectoryName,

        IncludeSubdirectories = true
      };

      //watcher.Filter = "*.txt";

      // Add event handlers.
      watcher.Changed += OnChanged;
      watcher.Created += OnChanged;
      //watcher.Deleted += OnChanged;
      watcher.Renamed += OnRenamed;

      // Begin watching.
      watcher.EnableRaisingEvents = true;

      return watcher;
    }

    private void OnRenamed(object sender, RenamedEventArgs e)
    {
      Interlocked.Increment(ref _changesInUploadFolder);
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
      Interlocked.Increment(ref _changesInUploadFolder);
    }

    protected virtual void Dispose(bool disposing)
    {
      if (!disposedValue)
      {
        if (disposing)
        {
          _watcher.Dispose();
        }

        disposedValue = true;
      }
    }

    // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
    // ~UploadFileSystemWatcher()
    // {
    //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
    //     Dispose(disposing: false);
    // }

    public void Dispose()
    {
      // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
      Dispose(disposing: true);
      GC.SuppressFinalize(this);
    }
  }
}
