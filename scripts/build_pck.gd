extends SceneTree

const MOD_ID := "Sts2MultiplayerTrade"
const PACK_ROOT := "res://pack_assets/%s" % MOD_ID
const EXTERNAL_MANIFEST_PATH := "res://Sts2MultiplayerTrade.json"
const INTERNAL_MANIFEST_TARGET := "res://mod_manifest.json"

func _initialize() -> void:
    var args := OS.get_cmdline_user_args()
    if args.is_empty():
        push_error("Missing output .pck path.")
        quit(1)
        return

    var output_path := args[0]
    var packer := PCKPacker.new()
    var err := packer.pck_start(output_path)
    if err != OK:
        push_error("Failed to start pck build: %s" % err)
        quit(err)
        return

    var files := {}
    _collect_files(ProjectSettings.globalize_path(PACK_ROOT), "res://%s" % MOD_ID, files)
    _collect_imported_dependencies(files)
    for target_path in files.keys():
        var source_path: String = files[target_path]
        err = packer.add_file(target_path, source_path)
        if err != OK:
            push_error("Failed to add %s -> %s (%s)" % [source_path, target_path, err])
            quit(err)
            return

    var manifest_source := _build_internal_manifest_file()
    err = packer.add_file(INTERNAL_MANIFEST_TARGET, manifest_source)
    if err != OK:
        push_error("Failed to add internal mod manifest: %s" % err)
        quit(err)
        return

    err = packer.flush()
    if err != OK:
        push_error("Failed to finalize pck: %s" % err)
        quit(err)
        return

    print("Built pck: %s" % output_path)
    quit(0)

func _collect_files(source_dir: String, target_dir: String, files: Dictionary) -> void:
    var dir := DirAccess.open(source_dir)
    if dir == null:
        push_error("Missing pack source dir: %s" % source_dir)
        quit(2)
        return

    dir.list_dir_begin()
    while true:
        var name := dir.get_next()
        if name == "":
            break
        if name.begins_with("."):
            continue
        if name.begins_with("cover_source."):
            continue

        var source_path := source_dir.path_join(name)
        var target_path := target_dir.path_join(name)
        if dir.current_is_dir():
            _collect_files(source_path, target_path, files)
        else:
            files[target_path] = source_path
    dir.list_dir_end()

func _collect_imported_dependencies(files: Dictionary) -> void:
    var target_paths := files.keys()
    for target_path_variant in target_paths:
        var target_path := String(target_path_variant)
        if not target_path.ends_with(".import"):
            continue

        var import_source_path := String(files[target_path])
        var config := ConfigFile.new()
        var err := config.load(import_source_path)
        if err != OK:
            push_error("Failed to read import metadata %s (%s)" % [import_source_path, err])
            quit(err)
            return

        var dest_files_variant: Variant = config.get_value("deps", "dest_files", [])
        if dest_files_variant is not Array:
            continue

        for dest_file_variant in dest_files_variant:
            var dest_file := String(dest_file_variant)
            if dest_file == "":
                continue

            var source_path := ProjectSettings.globalize_path(dest_file)
            if not FileAccess.file_exists(dest_file):
                push_error("Missing imported resource %s referenced by %s. Run Godot import before packing." % [dest_file, import_source_path])
                quit(3)
                return

            files[dest_file] = source_path

func _build_internal_manifest_file() -> String:
    var external_manifest := _load_external_manifest()
    var internal_manifest := {
        "pck_name": MOD_ID,
        "name": String(external_manifest.get("name", MOD_ID)),
        "author": String(external_manifest.get("author", "Unknown")),
        "description": String(external_manifest.get("description", "")),
        "version": String(external_manifest.get("version", "0.0.0"))
    }

    var temp_path := ProjectSettings.globalize_path("user://generated_mod_manifest.json")
    var file := FileAccess.open(temp_path, FileAccess.WRITE)
    if file == null:
        push_error("Failed to create generated mod manifest at %s" % temp_path)
        quit(4)
        return ""

    file.store_string(JSON.stringify(internal_manifest, "\t"))
    file.flush()
    return temp_path

func _load_external_manifest() -> Dictionary:
    if not FileAccess.file_exists(EXTERNAL_MANIFEST_PATH):
        return {}

    var json := FileAccess.get_file_as_string(EXTERNAL_MANIFEST_PATH)
    var parsed: Variant = JSON.parse_string(json)
    if parsed is Dictionary:
        return parsed

    return {}
