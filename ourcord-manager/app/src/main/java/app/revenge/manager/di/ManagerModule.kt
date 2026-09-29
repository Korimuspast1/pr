package app.ourcord.manager.di

import app.ourcord.manager.domain.manager.DownloadManager
import app.ourcord.manager.domain.manager.InstallManager
import app.ourcord.manager.domain.manager.PreferenceManager
import org.koin.core.module.dsl.singleOf
import org.koin.dsl.module

val managerModule = module {
    singleOf(::DownloadManager)
    singleOf(::PreferenceManager)
    singleOf(::InstallManager)
}