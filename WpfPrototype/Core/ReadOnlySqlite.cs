using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

namespace AchievementTimelineReplay
{
    // Uses Windows' SQLite system library; no executable or user-supplied DLL is loaded.
    internal sealed class ReadOnlySqlite : IDisposable
    {
        const string Library="atr_system_sqlite";
        IntPtr database;
        static ReadOnlySqlite()
        {
            NativeLibrary.SetDllImportResolver(typeof(ReadOnlySqlite).Assembly,(name,assembly,path)=>name==Library?NativeLibrary.Load(OperatingSystem.IsWindows()?Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"winsqlite3.dll"):"libsqlite3.so.0"):IntPtr.Zero);
        }
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_open_v2([MarshalAs(UnmanagedType.LPUTF8Str)]string path,out IntPtr db,int flags,IntPtr vfs);
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_close(IntPtr db);
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl)]static extern IntPtr sqlite3_errmsg(IntPtr db);
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_busy_timeout(IntPtr db,int milliseconds);
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_prepare_v2(IntPtr db,[MarshalAs(UnmanagedType.LPUTF8Str)]string sql,int bytes,out IntPtr statement,IntPtr tail);
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_step(IntPtr statement);
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_finalize(IntPtr statement);
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_column_count(IntPtr statement);
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl)]static extern IntPtr sqlite3_column_name(IntPtr statement,int column);
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl)]static extern IntPtr sqlite3_column_text(IntPtr statement,int column);
        public ReadOnlySqlite(string path)
        {
            if(!File.Exists(path))throw new FileNotFoundException("No se encontró la base Playnite Achievements.",path);
            int rc=sqlite3_open_v2(Path.GetFullPath(path),out database,1,IntPtr.Zero);
            if(rc!=0){string error=Error();Dispose();throw new InvalidDataException(error);}
            sqlite3_busy_timeout(database,2000);
        }
        string Error()=>"No se pudo leer Playnite Achievements: "+Marshal.PtrToStringUTF8(sqlite3_errmsg(database))+". Cerrá Playnite antes de volver a intentar.";
        public List<Dictionary<string,string>> Query(string sql,CancellationToken token=default)
        {
            IntPtr statement=IntPtr.Zero;
            try
            {
                if(sqlite3_prepare_v2(database,sql,-1,out statement,IntPtr.Zero)!=0)throw new InvalidDataException(Error());
                int count=sqlite3_column_count(statement);var names=new string[count];for(int i=0;i<count;i++)names[i]=Marshal.PtrToStringUTF8(sqlite3_column_name(statement,i));
                var rows=new List<Dictionary<string,string>>();int rc;
                while((rc=sqlite3_step(statement))==100)
                {
                    token.ThrowIfCancellationRequested();if(rows.Count>=1000000)throw new InvalidDataException("La consulta Playnite Achievements supera el límite de registros.");
                    var row=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
                    for(int i=0;i<count;i++)row[names[i]]=Marshal.PtrToStringUTF8(sqlite3_column_text(statement,i));rows.Add(row);
                }
                if(rc!=101)throw new InvalidDataException(Error());return rows;
            }
            finally{if(statement!=IntPtr.Zero)sqlite3_finalize(statement);}
        }
        public void Dispose(){if(database!=IntPtr.Zero){sqlite3_close(database);database=IntPtr.Zero;}}
    }
}
