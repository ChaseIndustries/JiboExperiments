.DEFAULT_GOAL := help

.PHONY: %

%:
	@$(MAKE) -C OpenJibo $@
