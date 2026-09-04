.PHONY: build test validate-content run-server run-client scenario benchmark container-config container-build container-up container-down

build:
	dotnet build Lyfe.sln
	cd client && npm run build

test:
	dotnet test Lyfe.sln
	cd client && npm test

validate-content:
	dotnet run --project tools/Lyfe.RuleTool -- validate rules/v1
	dotnet run --project tools/Lyfe.RuleTool -- validate-world world-packs/primordial-earth
	dotnet run --project tools/Lyfe.RuleTool -- compile rules/v1 world-packs/primordial-earth scenario.foundation-sandbox world.primordial-foundation

run-server:
	dotnet run --project src/Lyfe.Server

run-client:
	cd client && npm run dev

scenario:
	dotnet run --project scenarios/Lyfe.Scenarios

benchmark:
	dotnet run --project benchmarks/Lyfe.Benchmarks -c Release

container-config:
	docker compose config

container-build:
	docker compose build server

container-up:
	docker compose up --build server

container-down:
	docker compose down
