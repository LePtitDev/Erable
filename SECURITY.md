# Security policy

## Reporting a vulnerability

**Do not open a public issue** for a security problem. Use GitHub private reporting: the *Security* tab of the repository, then *Report a vulnerability*.

If possible, include:

- the Erable version (`/api/info`, or *Help* in the application) and the mode (desktop or server);
- the steps to reproduce, and what an attacker gains (read, write, execution, denial of service);
- any useful proof of concept.

Erable is maintained in limited free time: reports are read as soon as possible, without a guaranteed response time. Once a fixed version is published, the vulnerability is described in a GitHub security advisory, with your name if you wish.

## Supported versions

Only the **latest published version** receives security fixes: the desktop application updates itself, and the Docker image is updated by pulling the new tag (`docs/deployment.md`).

## Scope

The measures in place and their limits are described in `docs/architecture.md` §9 (security) and `docs/deployment.md` §4 (what the server protects). Out of scope: a server exposed without HTTPS, a malicious administrator, and access to the disk of the machine hosting the projects.
