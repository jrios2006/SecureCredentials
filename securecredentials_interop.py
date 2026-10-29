#!/usr/bin/env python3
"""
SecureCredentials v2 - Interoperabilidad AES-256-GCM.

Uso:
    python securecredentials_interop.py encrypt entrada.json salida.enc
    python securecredentials_interop.py decrypt entrada.enc salida.json

ATENCION: Script de laboratorio con clave integrada.
No distribuir ni incorporar a Git.
"""

import argparse
import base64
import json
import os
import secrets
import struct
import sys
from pathlib import Path

from cryptography.hazmat.primitives.ciphers.aead import AESGCM


# ============================================================
# CONFIGURACION DE PRUEBAS
# ============================================================

# Copiar el valor de KeyBase64 de SecureCredentials/EmbeddedKeyProvider.cs
KEY_BASE64 = "QchNkwoCIxiEPvhATaZNCThqImUIBtE7aNdu6EOYTVo="

# Debe coincidir con el identificador de C#
KEY_ID = "embedded-v1"

VERSION = 2
ALGORITHM = "AES-256-GCM"
PREFIX = b"SecureCredentials|2|AES-256-GCM|"


def get_key():
    """Convierte la clave Base64 de C# en 32 bytes."""
    key = base64.b64decode(KEY_BASE64, validate=True)

    if len(key) != 32:
        raise ValueError(
            "KEY_BASE64 debe representar exactamente 32 bytes"
        )

    return key


def aad_for(key_id):
    """Reproduce CredentialMetadata.CreateAuthenticatedData."""
    if not isinstance(key_id, str) or not key_id.strip():
        raise ValueError("KeyId no puede estar vacio")

    key_id_bytes = key_id.encode("utf-8")

    if len(key_id_bytes) > 256:
        raise ValueError("KeyId supera 256 bytes UTF-8")

    return (
        PREFIX
        + struct.pack(">I", len(key_id_bytes))
        + key_id_bytes
    )


def write_private(path, data):
    """Crea un archivo privado sin sobrescribir uno existente."""
    flags = os.O_WRONLY | os.O_CREAT | os.O_EXCL

    if hasattr(os, "O_NOFOLLOW"):
        flags |= os.O_NOFOLLOW

    fd = os.open(path, flags, 0o600)

    with os.fdopen(fd, "wb") as output:
        output.write(data)


def encrypt(input_path, output_path):
    """Cifra un archivo UTF-8 en formato SecureCredentials v2."""
    key = get_key()

    plaintext = Path(input_path).read_bytes()

    # Igual que C#: el contenido original debe ser UTF-8.
    plaintext.decode("utf-8")

    nonce = secrets.token_bytes(12)
    aad = aad_for(KEY_ID)

    encrypted = AESGCM(key).encrypt(
        nonce,
        plaintext,
        aad
    )

    envelope = {
        "Version": VERSION,
        "Algorithm": ALGORITHM,
        "KeyId": KEY_ID,
        "Nonce": base64.b64encode(nonce).decode("ascii"),
        "Ciphertext": base64.b64encode(
            encrypted[:-16]
        ).decode("ascii"),
        "Tag": base64.b64encode(
            encrypted[-16:]
        ).decode("ascii")
    }

    output = json.dumps(
        envelope,
        indent=2,
        ensure_ascii=False
    ).encode("utf-8") + b"\n"

    write_private(output_path, output)

    print(f"Archivo cifrado: {output_path}")
    print(f"KeyId: {KEY_ID}")
    print("Algoritmo: AES-256-GCM")
    print("Version: 2")


def decrypt(input_path, output_path):
    """Descifra un archivo SecureCredentials v2."""
    key = get_key()

    envelope = json.loads(
        Path(input_path).read_text(encoding="utf-8")
    )

    if envelope["Version"] != VERSION:
        raise ValueError("Version no compatible")

    if envelope["Algorithm"] != ALGORITHM:
        raise ValueError("Algoritmo no compatible")

    key_id = envelope["KeyId"]

    if key_id != KEY_ID:
        raise ValueError(
            f"KeyId no reconocido: {key_id}"
        )

    nonce = base64.b64decode(
        envelope["Nonce"], validate=True
    )

    ciphertext = base64.b64decode(
        envelope["Ciphertext"], validate=True
    )

    tag = base64.b64decode(
        envelope["Tag"], validate=True
    )

    if len(nonce) != 12:
        raise ValueError("Nonce incorrecto")

    if len(tag) != 16:
        raise ValueError("Tag incorrecto")

    aad = aad_for(key_id)

    plaintext = AESGCM(key).decrypt(
        nonce,
        ciphertext + tag,
        aad
    )

    # Verificar que el contenido es texto UTF-8.
    plaintext.decode("utf-8")

    write_private(output_path, plaintext)

    print(f"Archivo descifrado: {output_path}")
    print("Autenticacion AES-256-GCM correcta.")


def main():
    parser = argparse.ArgumentParser(
        description="SecureCredentials AES-256-GCM v2"
    )

    commands = parser.add_subparsers(
        dest="command",
        required=True
    )

    enc = commands.add_parser("encrypt")
    enc.add_argument("input_file")
    enc.add_argument("output_file")

    dec = commands.add_parser("decrypt")
    dec.add_argument("input_file")
    dec.add_argument("output_file")

    args = parser.parse_args()

    try:
        if args.command == "encrypt":
            encrypt(args.input_file, args.output_file)
        else:
            decrypt(args.input_file, args.output_file)

    except Exception as exc:
        print(
            f"Error: {type(exc).__name__}: {exc}",
            file=sys.stderr
        )
        return 1

    return 0


if __name__ == "__main__":
    sys.exit(main())
