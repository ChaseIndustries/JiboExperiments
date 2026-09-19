.DEFAULT_GOAL := help

.PHONY: help cloud clone prep health reconnect

# Live Mac + kitchen Jibo. From OpenJibo/: make clone (other terminal), then make cloud.
# Reconnect steps: make reconnect   or   docs/local-jibo-device-runbook.md

help:
	@printf '%s\n' \
	  '' \
	  'OpenJibo live cloud (this Mac, physical Jibo on the LAN)' \
	  '' \
	  '  make clone       clone TTS sidecar on 127.0.0.1:8091 (no sudo, other terminal)' \
	  '  make cloud       .NET OpenJibo on :443 (sudo, loads .env)' \
	  '  make prep        health check plus robot host names' \
	  '  make health      GET http://localhost:24605/health' \
	  '  make reconnect   print how to point Jibo at this cloud again' \
	  '' \
	  'Ctrl-C stops the process you started. Kill leftover :443 with:' \
	  '  sudo lsof -nP -iTCP:443 -sTCP:LISTEN' \
	  ''

clone:
	./scripts/cloud/start-jibo-clone-tts.sh

cloud:
	@bash -lc 'set -euo pipefail; \
	  cd "$(CURDIR)"; \
	  set -a; \
	  if [ -f .env ]; then . ./.env; fi; \
	  set +a; \
	  export OpenJibo__Robot__RobotId="$${OpenJibo__Robot__RobotId:-5a0b6398faa0f0001c5d0df1}"; \
	  echo "OpenJibo .NET cloud on 443. Loads .env. Password prompt is for sudo."; \
	  sudo -E env PATH="$$PATH" HOME="$$HOME" ./scripts/cloud/start-dotnet-with-node-cert.sh'

prep:
	./scripts/cloud/invoke-live-jibo-prep.sh

health:
	curl --silent --show-error --fail http://localhost:24605/health | python3 -m json.tool

reconnect:
	@printf '%s\n' \
	  '' \
	  '1. Other terminal:  make clone' \
	  '2. This terminal:   make cloud' \
	  '3. Health:          make health' \
	  '   or:              curl -k https://localhost/health' \
	  '4. On Jibo, hosts must resolve these names to THIS Mac LAN IP:' \
	  '     api.jibo.com' \
	  '     api-socket.jibo.com' \
	  '     open-jibo-socket.openjibo.com' \
	  '     neohub.openjibo.com' \
	  '5. Leave /var/jibo/credentials.json region as api' \
	  '   Never set it to openjibo-local.' \
	  '6. If the Mac DHCP address changed, update OPENJIBO_MAC_IP in' \
	  '   /opt/jibo/openjibo-bootstrap.sh and rerun that script on Jibo.' \
	  '7. From Jibo:  curl -k https://api.jibo.com/health' \
	  '8. Wake Jibo. Say hey Jibo, how are you?' \
	  '' \
	  'Full runbook: docs/local-jibo-device-runbook.md' \
	  ''
