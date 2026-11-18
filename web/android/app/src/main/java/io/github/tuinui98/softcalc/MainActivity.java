// MainActivity.java - The Android app's one screen: Capacitor's web view, after the start screen
//
// The start screen (the icon on the dark page colour) stays SPLASH_MS
// after the app starts, while the web page and the engine load behind it
// (owner 2026-11-19: one second longer than the moment the page first draws).

package io.github.tuinui98.softcalc;

import android.os.Bundle;
import android.os.SystemClock;

import androidx.core.splashscreen.SplashScreen;

import com.getcapacitor.BridgeActivity;

public class MainActivity extends BridgeActivity
{
	private static final long SPLASH_MS = 1000;

	@Override
	protected void onCreate(Bundle savedInstanceState)
	{
		// Before super.onCreate, which draws the page's theme
		SplashScreen splash = SplashScreen.installSplashScreen(this);
		final long until = SystemClock.uptimeMillis() + SPLASH_MS;
		splash.setKeepOnScreenCondition(() -> SystemClock.uptimeMillis() < until);
		super.onCreate(savedInstanceState);
	}
}
