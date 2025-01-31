build:
	dotnet publish .\src\ToggleRoundedCorners\ --configuration Release --output Build
	
help:
	@echo "Usage: make <target>"
	@echo ""
	@echo "Targets:"
	@echo "  build       Build the application"
	@echo "  help        Show this help message"