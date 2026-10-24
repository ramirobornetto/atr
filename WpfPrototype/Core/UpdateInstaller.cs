using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;

namespace AchievementTimelineReplay
{
    internal sealed class InstallBackup
    {
        public string Target;
        public string Backup;
        public readonly List<string> Existing=new List<string>();
        public readonly List<string> Created=new List<string>();
        public void Rollback()
        {
            var errors=new List<Exception>();
            foreach(string relative in Created.AsEnumerable().Reverse())
                try{string file=Path.Combine(Target,relative);if(File.Exists(file))File.Delete(file);}catch(Exception ex){errors.Add(ex);}
            foreach(string relative in Existing.AsEnumerable().Reverse())
                try{File.Copy(Path.Combine(Backup,relative),Path.Combine(Target,relative),true);}catch(Exception ex){errors.Add(ex);}
            if(errors.Count>0)throw new AggregateException("No se pudo restaurar completamente la versión anterior. La copia de seguridad permanece en "+Backup,errors);
        }
    }
    internal static class UpdateInstaller
    {
        static readonly HashSet<string> Reserved=new HashSet<string>(new[]{"Library","Plans","Snapshots","Origins","Updates","UpdateDrafts","Mappings","Xbox","Epic","Runs","history-choices.json","source-options.json","settings.txt"},StringComparer.OrdinalIgnoreCase);
        public static void Verify(string package,string sha256,long expectedSize)
        {
            if(new FileInfo(package).Length!=expectedSize)throw new InvalidDataException("El tamaño del ZIP de actualización no coincide.");
            using var stream=File.OpenRead(package);
            string actual=Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
            if(!String.Equals(actual,sha256,StringComparison.Ordinal))throw new InvalidDataException("El SHA256 del ZIP no coincide; no se instalará.");
        }
        public static void Extract(string package,string stage)
        {
            if(Directory.Exists(stage) && Directory.EnumerateFileSystemEntries(stage).Any())throw new IOException("La carpeta temporal de instalación no está vacía.");
            Directory.CreateDirectory(stage);string prefix=Path.GetFullPath(stage)+Path.DirectorySeparatorChar;
            var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);long expanded=0;
            using var archive=ZipFile.OpenRead(package);
            if(archive.Entries.Count>20000)throw new InvalidDataException("Demasiados archivos en el ZIP.");
            foreach(var entry in archive.Entries)
            {
                string normalized=entry.FullName.Replace('\\','/');var parts=normalized.TrimEnd('/').Split('/');
                if(Path.IsPathRooted(normalized) || parts.Length==0 || parts.Any(p=>String.IsNullOrEmpty(p) || p=="." || p==".." || p.Contains(':') || p.Contains('\0') || p.EndsWith(" ") || p.EndsWith(".")) || Reserved.Contains(parts[0]))throw new InvalidDataException("Ruta no permitida en el ZIP.");
                if(((entry.ExternalAttributes>>16)&0xF000)==0xA000)throw new InvalidDataException("No se admiten enlaces simbólicos en la actualización.");
                string destination=Path.GetFullPath(Path.Combine(stage,Path.Combine(parts)));
                if(!destination.StartsWith(prefix,StringComparison.OrdinalIgnoreCase) || !seen.Add(destination))throw new InvalidDataException("Ruta repetida o fuera de la carpeta temporal.");
                expanded=checked(expanded+entry.Length);
                if(expanded>700L*1024*1024)throw new InvalidDataException("ZIP descomprimido demasiado grande.");
                if(normalized.EndsWith('/'))Directory.CreateDirectory(destination);
                else{Directory.CreateDirectory(Path.GetDirectoryName(destination));entry.ExtractToFile(destination,false);}
            }
            foreach(string required in new[]{"AchievementTimelineReplay.Wpf.exe","AchievementTimelineReplay.Wpf.dll","ATR.Updater.exe","ATR.Updater.dll"})
                if(!File.Exists(Path.Combine(stage,required)))throw new InvalidDataException("La actualización no contiene "+required);
        }
        static void NoLinkedParents(string path,string root)
        {
            for(string directory=Path.GetDirectoryName(path);directory!=null;directory=Path.GetDirectoryName(directory))
            {
                if(Directory.Exists(directory) && (File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0)throw new IOException("No se actualizará una carpeta enlazada: "+directory);
                if(String.Equals(directory,root,StringComparison.OrdinalIgnoreCase))break;
            }
        }
        public static InstallBackup Install(string stage,string target,string backup)
        {
            target=Path.GetFullPath(target).TrimEnd(Path.DirectorySeparatorChar);
            if(String.IsNullOrWhiteSpace(target) || String.Equals(target,Path.GetPathRoot(target)?.TrimEnd(Path.DirectorySeparatorChar),StringComparison.OrdinalIgnoreCase))throw new IOException("La carpeta de instalación no puede ser la raíz del disco.");
            if(Directory.Exists(backup) && Directory.EnumerateFileSystemEntries(backup).Any())throw new IOException("La copia de seguridad ya contiene archivos.");
            Directory.CreateDirectory(backup);Directory.CreateDirectory(target);
            var result=new InstallBackup{Target=target,Backup=backup};
            try
            {
                foreach(string source in Directory.GetFiles(stage,"*",SearchOption.AllDirectories).OrderBy(p=>p,StringComparer.OrdinalIgnoreCase))
                {
                    string relative=Path.GetRelativePath(stage,source);string destination=Path.Combine(target,relative);
                    if(Reserved.Contains(relative.Split(Path.DirectorySeparatorChar)[0]))throw new IOException("El paquete intentó modificar datos de usuario.");
                    NoLinkedParents(destination,target);Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    if(File.Exists(destination))
                    {
                        if((File.GetAttributes(destination)&FileAttributes.ReparsePoint)!=0)throw new IOException("No se reemplazará un archivo enlazado.");
                        string original=Path.Combine(backup,relative);Directory.CreateDirectory(Path.GetDirectoryName(original));File.Copy(destination,original,false);result.Existing.Add(relative);
                    }
                    else result.Created.Add(relative);
                    File.Copy(source,destination,true);
                }
                return result;
            }
            catch(Exception installError)
            {
                try{result.Rollback();}catch(Exception rollbackError){throw new AggregateException(installError,rollbackError);}
                throw;
            }
        }
    }
}
