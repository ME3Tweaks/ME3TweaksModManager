#!/usr/bin/env python3

# Based on Steam-Metadata-Editor by tralph3
# Original license header:
# A Metadata Editor for Steam Applications
# Copyright (C) 2023  Tomás Ralph
#
# This program is free software: you can redistribute it and/or modify
# it under the terms of the GNU General Public License as published by
# the Free Software Foundation, either version 3 of the License, or
# (at your option) any later version.
#
# This program is distributed in the hope that it will be useful,
# but WITHOUT ANY WARRANTY; without even the implied warranty of
# MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
# GNU General Public License for more details.
#
# You should have received a copy of the GNU General Public License
# along with this program.  If not, see <https://www.gnu.org/licenses/>.

# This code is also licensed under the GNU General Public License v3.0 or later, as stated above.


"""Minimal non-GUI launcher option patcher for Steam appinfo.vdf.

Adds or updates one launch option named "ME3Tweaks Mod Manager" for the
provided app IDs ("pfx IDs").
"""

from __future__ import annotations

import argparse
import os
import re
from hashlib import sha1
from struct import pack, unpack
from typing import Dict, Iterable, List, Optional


APPINFO_29 = 0x107564429
APPINFO_28 = 0x107564428


class IncompatibleVDFError(Exception):
    def __init__(self, vdf_version):
        self.vdf_version = vdf_version


class Appinfo:
    def __init__(self, vdf_path, choose_apps=False, apps=None):
        self.offset = 0
        self.string_pool = []
        self.string_offset = 0

        self.version = 0
        self.vdf_path = vdf_path

        self.COMPATIBLE_VERSIONS = [APPINFO_29, APPINFO_28]

        self.SEPARATOR = b"\x00"
        self.TYPE_DICT = b"\x00"
        self.TYPE_STRING = b"\x01"
        self.TYPE_INT32 = b"\x02"
        self.SECTION_END = b"\x08"

        self.INT_SEPARATOR = int.from_bytes(self.SEPARATOR, "little")
        self.INT_TYPE_DICT = int.from_bytes(self.TYPE_DICT, "little")
        self.INT_TYPE_STRING = int.from_bytes(self.TYPE_STRING, "little")
        self.INT_TYPE_INT32 = int.from_bytes(self.TYPE_INT32, "little")
        self.INT_SECTION_END = int.from_bytes(self.SECTION_END, "little")

        with open(self.vdf_path, "rb") as vdf:
            self.appinfoData = bytearray(vdf.read())

        self.verify_vdf_version()
        if self.version == APPINFO_29:
            self.string_offset = self.read_int64()
            prev_offset = self.offset
            self.offset = self.string_offset
            string_count = self.read_uint32()
            for _ in range(string_count):
                self.string_pool.append(self.read_string())
            self.offset = prev_offset

        if choose_apps:
            self.parsedAppInfo = {}
            for app in apps:
                self.parsedAppInfo[app] = self.read_app(app)
        else:
            self.parsedAppInfo = self.read_all_apps()

    def read_string(self):
        str_end = self.appinfoData.find(self.INT_SEPARATOR, self.offset)
        string = self.appinfoData[self.offset:str_end]
        try:
            string = string.decode("utf-8")
        except UnicodeDecodeError:
            string = string.decode("latin-1")
        self.offset += str_end - self.offset + 1
        return string

    def read_string_appinfo29(self):
        index = self.read_uint32()
        return self.string_pool[index]

    def read_int64(self):
        int64 = unpack("<q", self.appinfoData[self.offset:self.offset + 8])[0]
        self.offset += 8
        return int64

    def read_uint64(self):
        int64 = unpack("<Q", self.appinfoData[self.offset:self.offset + 8])[0]
        self.offset += 8
        return int64

    def read_uint32(self):
        int32 = unpack("<I", self.appinfoData[self.offset:self.offset + 4])[0]
        self.offset += 4
        return int32

    def read_byte(self):
        byte = self.appinfoData[self.offset]
        self.offset += 1
        return byte

    def parse_subsections(self):
        subsection = {}
        value_parsers = {
            self.INT_TYPE_DICT: self.parse_subsections,
            self.INT_TYPE_STRING: self.read_string,
            self.INT_TYPE_INT32: self.read_uint32,
        }

        while True:
            value_type = self.read_byte()
            if value_type == self.INT_SECTION_END:
                break

            if self.version == APPINFO_29:
                key = self.read_string_appinfo29()
            else:
                key = self.read_string()
            value = value_parsers[value_type]()

            subsection[key] = value

        return subsection

    def read_header(self):
        keys = [
            "appid",
            "size",
            "state",
            "last_update",
            "access_token",
            "checksum_text",
            "change_number",
            "checksum_binary",
        ]

        formats = [
            ["<I", 4],
            ["<I", 4],
            ["<I", 4],
            ["<I", 4],
            ["<Q", 8],
            ["<20s", 20],
            ["<I", 4],
            ["<20s", 20],
        ]

        header_data = {}

        for fmt, key in zip(formats, keys):
            value = unpack(fmt[0], self.appinfoData[self.offset:self.offset + fmt[1]])[0]
            self.offset += fmt[1]
            header_data[key] = value

        return header_data

    def verify_vdf_version(self):
        self.version = self.read_uint64()
        if self.version not in self.COMPATIBLE_VERSIONS:
            raise IncompatibleVDFError(self.version)

    def read_app(self, app_id):
        byte_data = self.SECTION_END + pack("<I", app_id)
        self.offset = self.appinfoData.find(byte_data) + 1
        if self.offset == 0:
            raise KeyError(app_id)
        app = self.read_header()
        app["sections"] = self.parse_subsections()
        app["installed"] = False
        app["install_path"] = "."
        return app

    def stop_reading(self):
        if self.version == APPINFO_28:
            return not self.offset < len(self.appinfoData) - 4
        if self.version == APPINFO_29:
            return not self.offset < self.string_offset - 4
        raise IncompatibleVDFError(self.version)

    def read_all_apps(self):
        apps = {}
        while not self.stop_reading():
            app = self.read_header()
            app["sections"] = self.parse_subsections()
            app["installed"] = False
            app["install_path"] = "."
            apps[app["appid"]] = app
        return apps

    def encode_header(self, data):
        return pack(
            "<4IQ20sI20s",
            data["appid"],
            data["size"],
            data["state"],
            data["last_update"],
            data["access_token"],
            data["checksum_text"],
            data["change_number"],
            data["checksum_binary"],
        )

    def encode_string(self, string):
        if "\x06" in string:
            return string[:-1].encode("latin-1") + self.SEPARATOR
        return string.encode() + self.SEPARATOR

    def encode_uint32(self, integer):
        return pack("<I", integer)

    def encode_int64(self, integer):
        return pack("<q", integer)

    def encode_key_appinfo29(self, key):
        try:
            index = self.string_pool.index(key)
        except ValueError:
            self.string_pool.append(key)
            self.appinfoData += self.encode_string(key)
        index = self.string_pool.index(key)
        return self.encode_uint32(index)

    def encode_subsections(self, data):
        encoded_data = bytearray()
        for key, value in data.items():
            key = self.encode_string(key) if self.version == APPINFO_28 else self.encode_key_appinfo29(key)
            if isinstance(value, dict):
                encoded_data += self.TYPE_DICT + key + self.encode_subsections(value)
            elif isinstance(value, str):
                encoded_data += self.TYPE_STRING + key + self.encode_string(value)
            elif isinstance(value, int):
                encoded_data += self.TYPE_INT32 + key + self.encode_uint32(value)

        encoded_data += self.SECTION_END
        return encoded_data

    def get_text_checksum(self, data):
        formatted_data = self.dict_to_text_vdf(data)
        hsh = sha1(formatted_data)
        return hsh.digest()

    def get_binary_checksum(self, data):
        hsh = sha1(data)
        return hsh.digest()

    def update_header_size_and_checksums(self, appinfo, size, checksum_text, checksum_binary):
        appinfo["checksum_binary"] = checksum_binary
        appinfo["checksum_text"] = checksum_text
        appinfo["size"] = size
        return appinfo

    def update_string_offset_and_count(self):
        string_count = len(self.string_pool)
        encoded_string_count = self.encode_uint32(string_count)
        last_app_start_index = self.appinfoData.rfind(b"\x08\x00\x00\x00\x00")
        string_table_offset = last_app_start_index + 5
        encoded_offset = self.encode_int64(string_table_offset)
        self.appinfoData[8:16] = encoded_offset
        self.appinfoData[string_table_offset:string_table_offset + 4] = encoded_string_count

    def update_app(self, app_id):
        appinfo = self.parsedAppInfo[app_id]
        encoded_subsections = self.encode_subsections(appinfo["sections"])
        old_header = self.encode_header(appinfo)

        size = len(encoded_subsections) + len(old_header) - 8
        checksum_text = self.get_text_checksum(appinfo["sections"])
        checksum_binary = self.get_binary_checksum(encoded_subsections)

        app_location = self.appinfoData.find(old_header)
        app_end_location = app_location + appinfo["size"] + 8

        self.parsedAppInfo[app_id] = self.update_header_size_and_checksums(
            appinfo, size, checksum_text, checksum_binary
        )

        updated_header = self.encode_header(appinfo)

        if app_location != -1:
            self.appinfoData[app_location:app_end_location] = updated_header + encoded_subsections
        else:
            self.appinfoData.extend(updated_header + encoded_subsections)

    def write_data(self):
        if self.version == APPINFO_29:
            self.update_string_offset_and_count()
        with open(self.vdf_path, "wb") as vdf:
            vdf.write(self.appinfoData)

    def dict_to_text_vdf(self, data, number_of_tabs=0):
        formatted_data = b""
        tabs = b"\t" * number_of_tabs

        for key in data.keys():
            if isinstance(data[key], dict):
                number_of_tabs += 1
                formatted_data += (
                    tabs
                    + b'"'
                    + key.replace("\\", "\\\\").encode()
                    + b'"'
                    + b"\n"
                    + tabs
                    + b"{"
                    + b"\n"
                    + self.dict_to_text_vdf(data[key], number_of_tabs)
                    + tabs
                    + b"}\n"
                )
                number_of_tabs -= 1
            else:
                if isinstance(data[key], str) and "\x06" in data[key]:
                    formatted_data += (
                        tabs
                        + b'"'
                        + key.replace("\\", "\\\\").encode()
                        + b'"'
                        + b"\t\t"
                        + b'"'
                        + data[key][:-1].replace("\\", "\\\\").encode("latin-1")
                        + b'"'
                        + b"\n"
                    )
                else:
                    formatted_data += (
                        tabs
                        + b'"'
                        + key.replace("\\", "\\\\").encode()
                        + b'"'
                        + b"\t\t"
                        + b'"'
                        + str(data[key]).replace("\\", "\\\\").encode()
                        + b'"'
                        + b"\n"
                    )

        return formatted_data


OPTION_NAME = "Run ME3Tweaks Mod Manager"
DEFAULT_WINDOWS_STEAM_PATH = r".steam/steam"


def parse_id_tokens(tokens: Iterable[str]) -> List[int]:
    ids: List[int] = []
    for token in tokens:
        parts = [part.strip() for part in token.split(",") if part.strip()]
        for part in parts:
            try:
                ids.append(int(part))
            except ValueError as exc:
                raise ValueError(f"Invalid pfx id '{part}'. Expected an integer.") from exc
    return ids


def read_library_paths(steam_path: str) -> List[str]:
    libraryfolders = os.path.join(steam_path, "steamapps", "libraryfolders.vdf")
    if not os.path.isfile(libraryfolders):
        return [steam_path]

    with open(libraryfolders, "r", encoding="utf-8", errors="ignore") as fh:
        content = fh.read()

    paths = re.findall(r'"path"\s+"(.*?)"', content)
    normalized = [path.replace("\\\\", "\\") for path in paths]

    if steam_path not in normalized:
        normalized.append(steam_path)

    return normalized


def find_install_path(steam_libraries: List[str], install_dir: str) -> Optional[str]:
    for library in steam_libraries:
        candidate = os.path.join(library, "steamapps", "common", install_dir)
        if os.path.isdir(candidate):
            return candidate
    return None


def to_launch_path(path: str, install_path: Optional[str]) -> str:
    if not path:
        return path

    # Steam launch entries generally work best as relative paths.
    if install_path and os.path.isabs(path):
        try:
            rel = os.path.relpath(path, install_path)
            if not rel.startswith("..") or rel.count("..") >= 1:
                path = rel
        except ValueError:
            pass

    if os.name == "nt":
        return path.replace("/", "\\")
    return path.replace("\\", "/")


def next_launch_slot(launch: Dict[str, dict]) -> str:
    numeric_keys = []
    for key in launch.keys():
        try:
            numeric_keys.append(int(key))
        except ValueError:
            continue

    if not numeric_keys:
        return "0"
    return str(max(numeric_keys) + 1)


def upsert_launch_option(
    app_sections: dict,
    executable: str,
    workingdir: str,
) -> None:
    appinfo_section = app_sections.setdefault("appinfo", {})
    config_section = appinfo_section.setdefault("config", {})
    launch = config_section.setdefault("launch", {})

    slot = None
    for key, value in launch.items():
        if isinstance(value, dict) and value.get("description") == OPTION_NAME:
            slot = key
            break

    if slot is None:
        slot = next_launch_slot(launch)

    launch[slot] = {
        "description": OPTION_NAME,
        "executable": executable,
        "workingdir": workingdir,
        "arguments": "",
        "config": {"oslist": "windows"},
    }


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description=(
            "Add/update one launch option named 'ME3Tweaks Mod Manager' for "
            "the given app IDs in Steam appinfo.vdf"
        )
    )
    parser.add_argument(
        "--pfx-ids",
        nargs="+",
        required=True,
        help="List of IDs. Supports space-separated and/or comma-separated values.",
    )
    parser.add_argument(
        "--exe",
        required=True,
        help="Path to the ME3Tweaks Mod Manager executable.",
    )
    parser.add_argument(
        "--steam-path",
        default=DEFAULT_WINDOWS_STEAM_PATH,
        help="Steam root path (default: %(default)s).",
    )
    return parser


def main() -> int:
    args = build_parser().parse_args()

    try:
        target_ids = parse_id_tokens(args.pfx_ids)
    except ValueError as exc:
        print(f"Error: {exc}")
        return 2

    steam_path = os.path.abspath(args.steam_path)
    vdf_path = os.path.join(steam_path, "appcache", "appinfo.vdf")
    if not os.path.isfile(vdf_path):
        print(f"Error: appinfo.vdf not found at: {vdf_path}")
        return 2

    exe_input = os.path.abspath(args.exe)
    if not os.path.isfile(exe_input):
        print(f"Error: executable not found: {exe_input}")
        return 2

    try:
        appinfo = Appinfo(vdf_path)
    except IncompatibleVDFError as exc:
        print(f"Error: unsupported appinfo.vdf version: {exc.vdf_version:#08x}")
        return 2

    libraries = read_library_paths(steam_path)

    updated = []
    missing = []

    for app_id in target_ids:
        app = appinfo.parsedAppInfo.get(app_id)
        if not app:
            missing.append(app_id)
            continue

        sections = app["sections"]
        install_dir = (
            sections.get("appinfo", {}).get("config", {}).get("installdir", "")
        )
        install_path = find_install_path(libraries, install_dir) if install_dir else None

        launch_exe = to_launch_path(exe_input, install_path)
        launch_workingdir = to_launch_path(os.path.dirname(exe_input), install_path)

        upsert_launch_option(sections, launch_exe, launch_workingdir)
        appinfo.update_app(app_id)
        updated.append(app_id)

    if updated:
        appinfo.write_data()

    print(f"Updated apps: {updated if updated else 'none'}")
    if missing:
        print(f"Missing app IDs: {missing}")

    return 0 if updated else 1


if __name__ == "__main__":
    raise SystemExit(main())

