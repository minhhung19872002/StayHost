#!/usr/bin/env python3
"""Print the SQL that resets a production account's password.

Prints rather than executes: this touches the live users table, and a command
somebody read before running is a different thing from one a script ran on their
behalf. Copy the statement, look at it, then run it.

The scheme has to match StayHost.Domain/PasswordHasher.cs exactly, or the app
will reject a password that was set correctly and there is nothing in the logs
to say why: PBKDF2-SHA256, 210,000 iterations, 16-byte salt, 32-byte key, both
stored base64.

    python reset-admin-password.py "mat khau moi"
    python reset-admin-password.py "mat khau moi" --email someone@staylio.vn
"""
import argparse
import base64
import hashlib
import os
import sys

ITERATIONS = 210_000
SALT_BYTES = 16
KEY_BYTES = 32


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("password")
    ap.add_argument("--email", default="admin@staylio.vn")
    args = ap.parse_args()

    if len(args.password) < 8:
        print("Mat khau toi thieu 8 ky tu (Accounts.cs).", file=sys.stderr)
        return 1

    salt = os.urandom(SALT_BYTES)
    key = hashlib.pbkdf2_hmac("sha256", args.password.encode(), salt, ITERATIONS, dklen=KEY_BYTES)

    hash_b64 = base64.b64encode(key).decode()
    salt_b64 = base64.b64encode(salt).decode()

    # Single-quoted values: base64 uses + / = and never a quote, so this is safe
    # to paste as-is. The email is the only thing that varies, and it is matched
    # rather than interpolated into anything clever.
    print(f"""-- Dat lai mat khau cho {args.email}
-- Chay: ssh hung@14.225.83.93 'docker exec -i stayhost-db psql -U stayhost -d stayhost' <<'SQL'
update users
   set "PasswordHash" = '{hash_b64}',
       "PasswordSalt" = '{salt_b64}'
 where "Email" = '{args.email}';
-- SQL
-- Ket qua mong doi: UPDATE 1. UPDATE 0 nghia la sai email.""")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
