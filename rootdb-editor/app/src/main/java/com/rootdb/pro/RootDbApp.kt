package com.rootdb.pro

import android.app.Application
import com.rootdb.pro.data.RootShell

class RootDbApp : Application() {
    override fun onCreate() {
        super.onCreate()
        RootShell.init(this)
    }
}
