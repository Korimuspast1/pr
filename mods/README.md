# mods/

Drop the two mod archives here, then run the swap:

    python3 tools/swap_mod_sounds.py \
        mods/Yamaha_YZF-R15_earlybeta.zip \
        mods/Motocross_motorcycle.zip \
        -o mods/Motocross_motorcycle_R15sounds.zip

Add `--dry-run` first to preview the replacement plan without writing anything.

This folder is gitignored - the archives stay local and are never committed.
