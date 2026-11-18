// MainActivity.java - The Android app's one screen: Capacitor's web view, after the start screen
//
// Android's start screen (the icon on the dark page colour) hands over to
// the page's own (index.html: icon, name, version, dots), which stays while
// the engine loads (owner 2026-11-19). installSplashScreen makes the hand-over
// the same on Android 7 to 11 as on 12 and later.

package io.github.tuinui98.softcalc;

import android.os.Bundle;

import androidx.core.splashscreen.SplashScreen;

import com.getcapacitor.BridgeActivity;

public class MainActivity extends BridgeActivity
{
	@Override
	protected void onCreate(Bundle savedInstanceState)
	{
		// Before super.onCreate, which sets the page's theme
		SplashScreen.installSplashScreen(this);
		super.onCreate(savedInstanceState);
	}
}
