package com.solana.unity.mwa;

import android.app.Activity;
import android.app.PendingIntent;
import android.content.BroadcastReceiver;
import android.content.ComponentName;
import android.content.Context;
import android.content.Intent;
import android.content.IntentFilter;
import android.os.Build;

/** System wallet chooser + {@link Intent#EXTRA_CHOSEN_COMPONENT} capture. No manifest entry */
public final class MwaChooserHelper {

    private static final String ACTION_CHOSEN = "com.solana.unity.mwa.WALLET_CHOSEN";

    // RECEIVER_NOT_EXPORTED (API 33) as literal for older compileSdk.
    private static final int RECEIVER_NOT_EXPORTED = 0x4;

    private static volatile String sChosenPackage = null;
    private static BroadcastReceiver sReceiver = null;

    private MwaChooserHelper() {}

    /** Last chosen package, then clear. Null if none. */
    public static synchronized String consumeChosenPackage() {
        String pkg = sChosenPackage;
        sChosenPackage = null;
        return pkg;
    }

    /** createChooser + IntentSender callback = launches MWA target intent */
    public static void launchWithChooser(Activity activity, Intent target, String title) {
        sChosenPackage = null;
        registerReceiver(activity.getApplicationContext());

        Intent broadcast = new Intent(ACTION_CHOSEN).setPackage(activity.getPackageName());

        int flags = PendingIntent.FLAG_UPDATE_CURRENT;
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.S) {
            flags |= PendingIntent.FLAG_MUTABLE;
        }
        PendingIntent pendingIntent = PendingIntent.getBroadcast(activity, 0, broadcast, flags);

        Intent chooser = Intent.createChooser(target, title, pendingIntent.getIntentSender());
        activity.startActivity(chooser);
    }

    private static synchronized void registerReceiver(Context context) {
        if (sReceiver != null) {
            return;
        }
        sReceiver = new BroadcastReceiver() {
            @Override
            public void onReceive(Context ctx, Intent intent) {
                ComponentName chosen = intent.getParcelableExtra(Intent.EXTRA_CHOSEN_COMPONENT);
                if (chosen != null) {
                    sChosenPackage = chosen.getPackageName();
                }
            }
        };
        IntentFilter filter = new IntentFilter(ACTION_CHOSEN);
        if (Build.VERSION.SDK_INT >= 33) {
            context.registerReceiver(sReceiver, filter, RECEIVER_NOT_EXPORTED);
        } else {
            context.registerReceiver(sReceiver, filter);
        }
    }
}
