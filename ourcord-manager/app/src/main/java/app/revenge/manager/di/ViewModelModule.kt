package app.ourcord.manager.di

import app.ourcord.manager.ui.viewmodel.home.HomeViewModel
import app.ourcord.manager.ui.viewmodel.installer.InstallerViewModel
import app.ourcord.manager.ui.viewmodel.installer.LogViewerViewModel
import app.ourcord.manager.ui.viewmodel.libraries.LibrariesViewModel
import app.ourcord.manager.ui.viewmodel.settings.AdvancedSettingsViewModel
import org.koin.core.module.dsl.factoryOf
import org.koin.dsl.module

val viewModelModule = module {
    factoryOf(::InstallerViewModel)
    factoryOf(::AdvancedSettingsViewModel)
    factoryOf(::HomeViewModel)
    factoryOf(::LogViewerViewModel)
    factoryOf(::LibrariesViewModel)
}