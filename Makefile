build:
	dotnet publish --configuration Release --output Build .\src\ToggleRoundedCorners\
	
help:
	@echo "Usage: make <target>"
	@echo ""
	@echo "Targets:"
	@echo "  build       Build the application"
	@echo "  help        Show this help message"