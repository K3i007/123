$ErrorActionPreference = "Stop"
$baseUrl = "http://localhost:5080"
$values = @{}
Get-Content ".env" | ForEach-Object { if ($_ -match '^([^#=]+)=(.*)$') { $values[$matches[1]] = $matches[2] } }
$password = if ($values["SeedRolePassword"]) { $values["SeedRolePassword"] } else { $values["SeedAdmin__Password"] }
function Login([string]$email) { (Invoke-RestMethod "$baseUrl/api/v1/auth/login" -Method Post -ContentType "application/json" -Body (@{ email = $email; password = $password } | ConvertTo-Json)).accessToken }
function ExpectStatus([int]$expected, [scriptblock]$action) { $response = & $action; if ([int]$response.StatusCode -ne $expected) { throw "Expected HTTP $expected, got $($response.StatusCode)." }; Write-Host "PASS HTTP $expected"; return $response }
function ResponseText($response) { if ($response.Content -is [byte[]]) { return [System.Text.Encoding]::UTF8.GetString($response.Content) }; return [string]$response.Content }
$inventory = @{ Authorization = "Bearer $(Login ($values["SeedInventoryEmail"] ?? "inventory@concesionaria.local"))" }
$sales = @{ Authorization = "Bearer $(Login ($values["SeedSalesEmail"] ?? "sales@concesionaria.local"))" }
$make = (Invoke-RestMethod "$baseUrl/api/v1/inventory/makes" -Headers $inventory)[0]
$model = (Invoke-RestMethod "$baseUrl/api/v1/inventory/models?makeId=$($make.id)" -Headers $inventory)[0]
$branch = (Invoke-RestMethod "$baseUrl/api/v1/inventory/branches" -Headers $inventory)[0]
$variant = (Invoke-RestMethod "$baseUrl/api/v1/inventory/variants?modelId=$($model.id)" -Headers $inventory)[0]
$vehicle = (Invoke-RestMethod "$baseUrl/api/v1/inventory/vehicles?makeId=$($make.id)&status=Draft&pageSize=1" -Headers $inventory).items[0]
$body = @{ branchId = $branch.id; makeId = $make.id; modelId = $model.id; variantId = $variant.id; year = $vehicle.year; mileage = $vehicle.mileage; price = ([decimal]$vehicle.price + 1); currency = $vehicle.currency; vin = $vehicle.vin; plate = $vehicle.plate; condition = $vehicle.condition; color = $vehicle.color; transmission = $vehicle.transmission; fuel = $vehicle.fuel; drivetrain = $vehicle.drivetrain; bodyStyle = $vehicle.bodyStyle; customFields = $vehicle.customFields; priceReason = "HTTP concurrency test" } | ConvertTo-Json -Compress
$forbidden = ExpectStatus 403 { Invoke-WebRequest "$baseUrl/api/v1/inventory/vehicles" -Method Post -Headers $sales -ContentType "application/json" -Body "{}" -SkipHttpErrorCheck }
$missingVersion = ExpectStatus 428 { Invoke-WebRequest "$baseUrl/api/v1/inventory/vehicles/$($vehicle.id)" -Method Put -Headers $inventory -ContentType "application/json" -Body $body -SkipHttpErrorCheck }
$headersWithVersion = @{ Authorization = $inventory.Authorization; "If-Match" = "`"$($vehicle.version)`"" }
$updated = Invoke-WebRequest "$baseUrl/api/v1/inventory/vehicles/$($vehicle.id)" -Method Put -Headers $headersWithVersion -ContentType "application/json" -Body $body -SkipHttpErrorCheck
if ([int]$updated.StatusCode -ne 200) { throw "Setup update for stale-version test failed with HTTP $($updated.StatusCode): $($updated.Content)" }
$staleBody = $body | ConvertFrom-Json
$staleBody.price = [decimal]$staleBody.price + 1
$stale = ExpectStatus 409 { Invoke-WebRequest "$baseUrl/api/v1/inventory/vehicles/$($vehicle.id)" -Method Put -Headers $headersWithVersion -ContentType "application/json" -Body ($staleBody | ConvertTo-Json -Compress) -SkipHttpErrorCheck }
Write-Output "428 body: $(ResponseText $missingVersion)"
Write-Output "409 body: $(ResponseText $stale)"
Write-Output "Inventory HTTP authorization/precondition checks passed."
