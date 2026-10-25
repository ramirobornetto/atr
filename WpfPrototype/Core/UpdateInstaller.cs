using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Buffers.Binary;
using System.Reflection.PortableExecutable;
using System.Reflection;
using System.Text.Json;
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
                try{Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(Target,relative)));File.Copy(Path.Combine(Backup,relative),Path.Combine(Target,relative),true);}catch(Exception ex){errors.Add(ex);}
            if(errors.Count>0)throw new AggregateException(UiText.Text("No se pudo restaurar completamente la versión anterior. La copia de seguridad permanece en ")+Backup,errors);
        }
    }
    internal static class UpdateInstaller
    {
        static readonly HashSet<string> Reserved=new HashSet<string>(new[]{"Library","Plans","Snapshots","Origins","Updates","UpdateDrafts","Mappings","Xbox","Epic","Runs","history-choices.json","source-options.json","settings.txt","ui-language.txt"},StringComparer.OrdinalIgnoreCase);
        public static void Verify(string package,string sha256,long expectedSize)
        {
            if(new FileInfo(package).Length!=expectedSize)throw new InvalidDataException(UiText.Text("El tamaño del ZIP de actualización no coincide."));
            using var stream=File.OpenRead(package);
            string actual=Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
            if(!String.Equals(actual,sha256,StringComparison.Ordinal))throw new InvalidDataException(UiText.Text("El SHA256 del ZIP no coincide; no se instalará."));
        }
        public static void Extract(string package,string stage)
        {
            if(Directory.Exists(stage) && Directory.EnumerateFileSystemEntries(stage).Any())throw new IOException(UiText.Text("La carpeta temporal de instalación no está vacía."));
            Directory.CreateDirectory(stage);string prefix=Path.GetFullPath(stage)+Path.DirectorySeparatorChar;
            var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);long expanded=0;
            using var archive=ZipFile.OpenRead(package);
            if(archive.Entries.Count>20000)throw new InvalidDataException(UiText.Text("Demasiados archivos en el ZIP."));
            foreach(var entry in archive.Entries)
            {
                string normalized=entry.FullName.Replace('\\','/');var parts=normalized.TrimEnd('/').Split('/');
                if(Path.IsPathRooted(normalized) || parts.Length==0 || parts.Any(p=>String.IsNullOrEmpty(p) || p=="." || p==".." || p.Contains(':') || p.Contains('\0') || p.EndsWith(" ") || p.EndsWith(".")) || Reserved.Contains(parts[0]))throw new InvalidDataException(UiText.Text("Ruta no permitida en el ZIP."));
                if(((entry.ExternalAttributes>>16)&0xF000)==0xA000)throw new InvalidDataException(UiText.Text("No se admiten enlaces simbólicos en la actualización."));
                string destination=Path.GetFullPath(Path.Combine(stage,Path.Combine(parts)));
                if(!destination.StartsWith(prefix,StringComparison.OrdinalIgnoreCase) || !seen.Add(destination))throw new InvalidDataException(UiText.Text("Ruta repetida o fuera de la carpeta temporal."));
                expanded=checked(expanded+entry.Length);
                if(expanded>700L*1024*1024)throw new InvalidDataException(UiText.Text("ZIP descomprimido demasiado grande."));
                if(normalized.EndsWith('/'))Directory.CreateDirectory(destination);
                else{Directory.CreateDirectory(Path.GetDirectoryName(destination));entry.ExtractToFile(destination,false);}
            }
            if(File.Exists(Path.Combine(stage,"AchievementTimelineReplay.exe")))
            {
                var metadata=ReadCompactPackage(stage);
                if(metadata.Layout!=2 || !Version.TryParse(metadata.Version,out _))throw new InvalidDataException("Invalid compact package metadata.");
            }
            else foreach(string required in new[]{"AchievementTimelineReplay.Wpf.exe","AchievementTimelineReplay.Wpf.dll","ATR.Updater.exe","ATR.Updater.dll"})
                if(!File.Exists(Path.Combine(stage,required)))throw new InvalidDataException(UiText.Text("La actualización no contiene ")+required);
        }
        internal sealed class CompactPackage
        {
            public int Layout {get;set;}
            public string Version {get;set;}
            public Dictionary<string,string[]> LegacyFiles {get;set;}=new Dictionary<string,string[]>();
        }
        static CompactPackage ReadCompactPackage(string directory)
        {
            string path=Path.Combine(directory,"Support","package.json");
            if(!File.Exists(path) || new FileInfo(path).Length>2*1024*1024)throw new InvalidDataException("Missing or oversized compact package metadata.");
            return JsonSerializer.Deserialize<CompactPackage>(File.ReadAllText(path))??throw new InvalidDataException("Invalid compact package metadata.");
        }
        public static string ApplicationPath(string directory)=>Path.Combine(directory,File.Exists(Path.Combine(directory,"AchievementTimelineReplay.exe"))?"AchievementTimelineReplay.exe":"AchievementTimelineReplay.Wpf.exe");
        public static Version ApplicationVersion(string directory)
        {
            string executable=Path.Combine(directory,"AchievementTimelineReplay.exe");
            if(!File.Exists(executable))return AssemblyName.GetAssemblyName(Path.Combine(directory,"AchievementTimelineReplay.Wpf.dll")).Version;
            var version=ReadExecutableVersion(executable);
            if(ReadCompactPackage(directory).Version!=version.ToString(3))throw new InvalidDataException("Compact executable and package versions differ.");
            return version;
        }
        static Version ReadExecutableVersion(string executable)
        {
            // Read RT_VERSION from the PE resource tree. FileVersionInfo cannot read
            // native Windows apphosts on Unix, where release packages are verified.
            using var file=File.OpenRead(executable);using var pe=new PEReader(file);
            var directory=pe.PEHeaders.PEHeader.ResourceTableDirectory;
            if(directory.Size<16 || directory.Size>16*1024*1024)throw new InvalidDataException("Invalid executable resources.");
            byte[] resources=pe.GetSectionData(directory.RelativeVirtualAddress).GetContent(0,directory.Size).ToArray();
            int Entry(int offset,int? id)
            {
                if(offset<0 || offset>resources.Length-16)throw new InvalidDataException("Invalid resource directory.");
                int count=BinaryPrimitives.ReadUInt16LittleEndian(resources.AsSpan(offset+12))+BinaryPrimitives.ReadUInt16LittleEndian(resources.AsSpan(offset+14));
                for(int i=0;i<count;i++)
                {
                    int entry=checked(offset+16+i*8);
                    if(entry>resources.Length-8)throw new InvalidDataException("Invalid resource entry.");
                    uint name=BinaryPrimitives.ReadUInt32LittleEndian(resources.AsSpan(entry));
                    if(id==null || name==(uint)id.Value)return unchecked((int)BinaryPrimitives.ReadUInt32LittleEndian(resources.AsSpan(entry+4)));
                }
                throw new InvalidDataException("Missing executable version resource.");
            }
            int type=Entry(0,16);if(type>=0)throw new InvalidDataException("Invalid version resource tree.");
            int name=Entry(type&0x7fffffff,null);if(name>=0)throw new InvalidDataException("Invalid version resource name.");
            int data=Entry(name&0x7fffffff,null);
            if(data<0 || data>resources.Length-16)throw new InvalidDataException("Invalid version resource data.");
            int rva=BinaryPrimitives.ReadInt32LittleEndian(resources.AsSpan(data)),size=BinaryPrimitives.ReadInt32LittleEndian(resources.AsSpan(data+4));
            if(size<24 || size>65536)throw new InvalidDataException("Invalid version resource length.");
            var blob=pe.GetSectionData(rva).GetContent(0,size).ToArray();
            for(int offset=0;offset<=blob.Length-24;offset+=4)
                if(BinaryPrimitives.ReadUInt32LittleEndian(blob.AsSpan(offset))==0xFEEF04BD && BinaryPrimitives.ReadUInt32LittleEndian(blob.AsSpan(offset+4))==0x10000)
                {
                    uint high=BinaryPrimitives.ReadUInt32LittleEndian(blob.AsSpan(offset+8)),low=BinaryPrimitives.ReadUInt32LittleEndian(blob.AsSpan(offset+12));
                    return new Version((int)(high>>16),(int)(high&0xffff),(int)(low>>16));
                }
            throw new InvalidDataException("Missing executable version value.");
        }
        static void RemoveLegacyFiles(string stage,string target,InstallBackup result)
        {
            if(!File.Exists(Path.Combine(stage,"AchievementTimelineReplay.exe")))return;
            var metadata=ReadCompactPackage(stage);
            if(metadata.LegacyFiles==null || metadata.LegacyFiles.Count>20000)throw new InvalidDataException("Invalid legacy file inventory.");
            foreach(var pair in metadata.LegacyFiles)
            {
                var parts=pair.Key.Replace('\\','/').Split('/');
                if(parts.Any(p=>String.IsNullOrEmpty(p) || p=="." || p==".." || p.Contains(':') || p.Contains('\0') || p.EndsWith(" ") || p.EndsWith(".")) || Reserved.Contains(parts[0]) || parts[0].Equals("Support",StringComparison.OrdinalIgnoreCase) || parts[0].Equals("AchievementTimelineReplay.exe",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Invalid legacy cleanup path.");
                string relative=Path.Combine(parts),file=Path.Combine(target,relative);
                NoLinkedParents(file,target);
                if(!File.Exists(file) || File.Exists(Path.Combine(stage,relative)))continue;
                if((File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0)throw new IOException("Linked legacy files cannot be removed.");
                string actual;
                using(var stream=File.OpenRead(file))actual=Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
                // Only byte-for-byte copies of files shipped by ATR are retired. Edited
                // documentation, sample histories and unrelated files are preserved.
                if(pair.Value==null || !pair.Value.Contains(actual,StringComparer.OrdinalIgnoreCase))continue;
                if(!result.Existing.Contains(relative,StringComparer.OrdinalIgnoreCase))
                {
                    string backup=Path.Combine(result.Backup,relative);Directory.CreateDirectory(Path.GetDirectoryName(backup));File.Copy(file,backup,false);result.Existing.Add(relative);
                }
                File.Delete(file);
                for(string directory=Path.GetDirectoryName(file);!String.Equals(directory,target,StringComparison.OrdinalIgnoreCase) && !Directory.EnumerateFileSystemEntries(directory).Any();directory=Path.GetDirectoryName(directory))Directory.Delete(directory);
            }
        }
        static void NoLinkedParents(string path,string root)
        {
            for(string directory=Path.GetDirectoryName(path);directory!=null;directory=Path.GetDirectoryName(directory))
            {
                if(Directory.Exists(directory) && (File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0)throw new IOException(UiText.Text("No se actualizará una carpeta enlazada: ")+directory);
                if(String.Equals(directory,root,StringComparison.OrdinalIgnoreCase))break;
            }
        }
        public static InstallBackup Install(string stage,string target,string backup)
        {
            target=Path.GetFullPath(target).TrimEnd(Path.DirectorySeparatorChar);
            if(String.IsNullOrWhiteSpace(target) || String.Equals(target,Path.GetPathRoot(target)?.TrimEnd(Path.DirectorySeparatorChar),StringComparison.OrdinalIgnoreCase))throw new IOException(UiText.Text("La carpeta de instalación no puede ser la raíz del disco."));
            if(Directory.Exists(backup) && Directory.EnumerateFileSystemEntries(backup).Any())throw new IOException(UiText.Text("La copia de seguridad ya contiene archivos."));
            Directory.CreateDirectory(backup);Directory.CreateDirectory(target);
            var result=new InstallBackup{Target=target,Backup=backup};
            try
            {
                foreach(string source in Directory.GetFiles(stage,"*",SearchOption.AllDirectories).OrderBy(p=>p,StringComparer.OrdinalIgnoreCase))
                {
                    string relative=Path.GetRelativePath(stage,source);string destination=Path.Combine(target,relative);
                    if(Reserved.Contains(relative.Split(Path.DirectorySeparatorChar)[0]))throw new IOException(UiText.Text("El paquete intentó modificar datos de usuario."));
                    NoLinkedParents(destination,target);Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    if(File.Exists(destination))
                    {
                        if((File.GetAttributes(destination)&FileAttributes.ReparsePoint)!=0)throw new IOException(UiText.Text("No se reemplazará un archivo enlazado."));
                        string original=Path.Combine(backup,relative);Directory.CreateDirectory(Path.GetDirectoryName(original));File.Copy(destination,original,false);result.Existing.Add(relative);
                    }
                    else result.Created.Add(relative);
                    File.Copy(source,destination,true);
                }
                RemoveLegacyFiles(stage,target,result);
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
