/*
 * FSpot.Utils.FileExtensions.cs
 *
 * Author(s)
 * 	Paul Wellner Bou <paul@purecodes.org>
 *
 * This is free software. See COPYING for details.
 */

using System;
using System.IO;
using Mono.Unix;
using GLib;

namespace FSpot.Utils
{
	public static class FileExtensions
	{
		public static bool CopyRecursive (this GLib.File source, GLib.File target, GLib.FileCopyFlags flags, GLib.Cancellable cancellable, GLib.FileProgressCallback callback)
		{
			bool result = true;
			
			GLib.FileType ft = source.QueryFileType (GLib.FileQueryInfoFlags.None, cancellable);
			
			Log.Debug (Catalog.GetString("Try to copy {0} -> {1}"), source.Path, target.Path);
			
			if (ft != GLib.FileType.Directory) {
				Log.Debug (Catalog.GetString("Copying {0} -> {1}"), source.Path, target.Path);
				return source.Copy (target, flags, cancellable, callback);
			}
			
			if (!target.Exists) {
				Log.Debug ("Creating directory: "+target.Path);
				result = result && target.MakeDirectoryWithParents (cancellable);
			}
			
			GLib.FileEnumerator fe = source.EnumerateChildren ("standard::name", GLib.FileQueryInfoFlags.None, cancellable);
			GLib.FileInfo fi = fe.NextFile ();
			while (fi != null) {
				GLib.File source_file = GLib.FileFactory.NewForPath (Path.Combine (source.Path, fi.Name));
				GLib.File target_file = GLib.FileFactory.NewForPath (Path.Combine (target.Path, fi.Name));
				Log.Debug (Catalog.GetString("CopyRecursive {0} -> {1}"), source_file.Path, target_file.Path);
				result = result && CopyRecursive(source_file, target_file, flags, cancellable, callback);
				fi = fe.NextFile ();
			}
			fe.Close (cancellable);
			return result;
		}
	}
}
