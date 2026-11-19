// DownloadsPlugin.java - Export's files into the device's Download/SoftCalc folder (owner 2026-11-19)
//
// The web view does not download, and the share sheet needs an app that
// saves files, which a device may not have. So Export writes straight to
// Download/SoftCalc: through MediaStore on Android 10 and later, which needs
// no permission, and as a plain file on Android 7 to 9, after asking for
// the storage permission (WRITE_EXTERNAL_STORAGE, declared up to SDK 28).
// The system's file chooser lists the folder, so Import finds the files.

package io.github.tuinui98.softcalc;

import android.Manifest;
import android.content.ContentResolver;
import android.content.ContentValues;
import android.net.Uri;
import android.os.Build;
import android.os.Environment;
import android.provider.MediaStore;
import android.util.Base64;

import com.getcapacitor.JSObject;
import com.getcapacitor.PermissionState;
import com.getcapacitor.Plugin;
import com.getcapacitor.PluginCall;
import com.getcapacitor.PluginMethod;
import com.getcapacitor.annotation.CapacitorPlugin;
import com.getcapacitor.annotation.Permission;
import com.getcapacitor.annotation.PermissionCallback;

import java.io.File;
import java.io.FileOutputStream;
import java.io.IOException;
import java.io.OutputStream;

@CapacitorPlugin(
	name = "Downloads",
	permissions = { @Permission(alias = "storage", strings = { Manifest.permission.WRITE_EXTERNAL_STORAGE }) }
)
public class DownloadsPlugin extends Plugin
{
	private static final String FOLDER = "SoftCalc";

	@PluginMethod
	public void save(PluginCall call)
	{
		if (Build.VERSION.SDK_INT < Build.VERSION_CODES.Q && getPermissionState("storage") != PermissionState.GRANTED)
		{
			requestPermissionForAlias("storage", call, "storageAnswered");
			return;
		}
		write(call);
	}

	@PermissionCallback
	private void storageAnswered(PluginCall call)
	{
		if (getPermissionState("storage") == PermissionState.GRANTED)
		{
			write(call);
		}
		else
		{
			call.reject("no permission to save files");
		}
	}

	private void write(PluginCall call)
	{
		String name = call.getString("name");
		String type = call.getString("type", "application/octet-stream");
		String data = call.getString("data");
		if (name == null || data == null)
		{
			call.reject("no file");
			return;
		}
		byte[] bytes = Base64.decode(data, Base64.DEFAULT);
		try
		{
			if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q)
			{
				writeMediaStore(name, type, bytes);
			}
			else
			{
				writeFile(name, bytes);
			}
			JSObject result = new JSObject();
			result.put("folder", Environment.DIRECTORY_DOWNLOADS + "/" + FOLDER);
			call.resolve(result);
		}
		catch (IOException | RuntimeException error)
		{
			call.reject(error.getMessage() != null ? error.getMessage() : "could not save " + name);
		}
	}

	private void writeMediaStore(String name, String type, byte[] bytes) throws IOException
	{
		ContentResolver resolver = getContext().getContentResolver();
		ContentValues values = new ContentValues();
		values.put(MediaStore.MediaColumns.DISPLAY_NAME, name);
		values.put(MediaStore.MediaColumns.MIME_TYPE, type);
		values.put(MediaStore.MediaColumns.RELATIVE_PATH, Environment.DIRECTORY_DOWNLOADS + "/" + FOLDER);
		values.put(MediaStore.MediaColumns.IS_PENDING, 1);
		Uri uri = resolver.insert(MediaStore.Downloads.EXTERNAL_CONTENT_URI, values);
		if (uri == null)
		{
			throw new IOException("could not create " + name);
		}
		try (OutputStream out = resolver.openOutputStream(uri))
		{
			if (out == null)
			{
				throw new IOException("could not open " + name);
			}
			out.write(bytes);
		}
		catch (IOException | RuntimeException error)
		{
			resolver.delete(uri, null, null);
			throw error;
		}
		values.clear();
		values.put(MediaStore.MediaColumns.IS_PENDING, 0);
		resolver.update(uri, values, null, null);
	}

	@SuppressWarnings("deprecation")    // the public folder's path is the way before Android 10
	private void writeFile(String name, byte[] bytes) throws IOException
	{
		File folder = new File(Environment.getExternalStoragePublicDirectory(Environment.DIRECTORY_DOWNLOADS), FOLDER);
		if (!folder.isDirectory() && !folder.mkdirs())
		{
			throw new IOException("could not make " + folder);
		}
		try (OutputStream out = new FileOutputStream(new File(folder, name)))
		{
			out.write(bytes);
		}
	}
}
