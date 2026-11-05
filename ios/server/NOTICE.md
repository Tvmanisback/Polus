# Third-party server notice

Polus iOS uses the upstream [Impostor](https://github.com/Impostor/Impostor) server runtime rather than reimplementing the Among Us network protocol.

- Upstream release: [Impostor v1.10.9](https://github.com/Impostor/Impostor/releases/tag/v1.10.9)
- Upstream license: GNU GPL-3.0
- Upstream compatibility stated by that release: Among Us 2024.6.18 through 19.0
- The download script retrieves the upstream release archive at setup time and verifies its published SHA-256 digest.
- The upstream server is not copied into this repository.

Polus-specific scripts and configuration in this folder remain separate from the upstream server source.
