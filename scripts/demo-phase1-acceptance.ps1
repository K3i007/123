$ErrorActionPreference = "Stop"
$baseUrl = "http://localhost:5080"
$values = @{}
Get-Content ".env" | ForEach-Object { if ($_ -match '^([^#=]+)=(.*)$') { $values[$matches[1]] = $matches[2] } }
$password = if ($values["SeedRolePassword"]) { $values["SeedRolePassword"] } else { $values["SeedAdmin__Password"] }

function ContentText($response) { if ($response.Content -is [byte[]]) { return [System.Text.Encoding]::UTF8.GetString($response.Content) }; return [string]$response.Content }
function Json($response) { (ContentText $response) | ConvertFrom-Json }
function Login([string]$email) {
    $result = Invoke-RestMethod "$baseUrl/api/v1/auth/login" -Method Post -ContentType "application/json" -Body (@{ email = $email; password = $password } | ConvertTo-Json)
    @{ Authorization = "Bearer $($result.accessToken)" }
}
function Request([string]$method, [string]$path, [hashtable]$headers, $body = $null) {
    $arguments = @{ Uri = "$baseUrl$path"; Method = $method; Headers = $headers; SkipHttpErrorCheck = $true }
    if ($null -ne $body) { $arguments.ContentType = "application/json"; $arguments.Body = ($body | ConvertTo-Json -Depth 6 -Compress) }
    Invoke-WebRequest @arguments
}
function Require([int]$expected, $response, [string]$label) {
    if ([int]$response.StatusCode -ne $expected) { throw "$label expected HTTP $expected but got $($response.StatusCode): $(ContentText $response)" }
    Write-Output "$label HTTP $expected"
}
function Subject([hashtable]$headers) {
    $token = $headers.Authorization.Substring(7).Split('.')[1].Replace('-', '+').Replace('_', '/')
    $token += '=' * ((4 - $token.Length % 4) % 4)
    (([System.Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($token))) | ConvertFrom-Json).sub
}

$inventory = Login ($values["SeedInventoryEmail"] ?? "inventory@concesionaria.local")
$manager = Login ($values["SeedManagerEmail"] ?? "manager@concesionaria.local")
$inventoryId = Subject $inventory
$managerId = Subject $manager
Write-Output "Seed users authenticated: InventoryManager=$inventoryId; Manager=$managerId"

$make = (Invoke-RestMethod "$baseUrl/api/v1/inventory/makes" -Headers $inventory)[0]
$model = (Invoke-RestMethod "$baseUrl/api/v1/inventory/models?makeId=$($make.id)" -Headers $inventory)[0]
$variant = (Invoke-RestMethod "$baseUrl/api/v1/inventory/variants?modelId=$($model.id)" -Headers $inventory)[0]
$branch = (Invoke-RestMethod "$baseUrl/api/v1/inventory/branches" -Headers $inventory)[0]
$suffix = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds().ToString().PadLeft(13, '0')
$newVehicle = @{ makeId = $make.id; modelId = $model.id; variantId = $variant.id; branchId = $branch.id; year = 2024; mileage = 1200; price = 0; currency = "MXN"; condition = "Used"; vin = "ACPT$suffix"; plate = "A$($suffix.Substring($suffix.Length - 6))"; color = "Azul"; transmission = "Manual"; fuel = "Gasolina"; drivetrain = $null; bodyStyle = "Sedán"; customFields = '{"doors":4}'; priceReason = "Alta inicial" }
$createdResponse = Request POST "/api/v1/inventory/vehicles" $inventory $newVehicle
Require 201 $createdResponse "Inventario crea vehículo"
$vehicle = Json $createdResponse
Write-Output "Created: id=$($vehicle.id) status=$($vehicle.status) price=$($vehicle.price) version=$($vehicle.version)"

foreach ($transition in @(@{ toStatus = "InReview"; reason = "Inventario inicia revisión" }, @{ toStatus = "Photography"; reason = "Inventario entrega a fotografía" })) {
    $response = Request POST "/api/v1/inventory/vehicles/$($vehicle.id)/transitions" $inventory $transition
    Require 200 $response "Inventario avanza a $($transition.toStatus)"
}
$forbiddenManual = Request POST "/api/v1/inventory/vehicles/$($vehicle.id)/transitions" $inventory @{ toStatus = "Inspection"; reason = "Intento no autorizado" }
Require 403 $forbiddenManual "Inventario intenta avance manual"

$inspection = Request POST "/api/v1/inventory/vehicles/$($vehicle.id)/transitions" $manager @{ toStatus = "Inspection"; reason = "Gerente confirma fotografía manual" }
Require 200 $inspection "Gerente avanza manualmente a Inspection"
$approved = Request POST "/api/v1/inventory/vehicles/$($vehicle.id)/transitions" $manager @{ toStatus = "Approved"; reason = "Gerente completa inspección manual" }
Require 200 $approved "Gerente avanza manualmente a Approved"
$invalidPublish = Request POST "/api/v1/inventory/vehicles/$($vehicle.id)/transitions" $manager @{ toStatus = "Published"; reason = "Intento sin precio" }
Require 400 $invalidPublish "Publicación con precio cero"
Write-Output "400 body: $(ContentText $invalidPublish)"

$current = Invoke-RestMethod "$baseUrl/api/v1/inventory/vehicles/$($vehicle.id)" -Headers $inventory
$newVehicle.price = 325000
$newVehicle.priceReason = "Precio autorizado para publicación"
$headersWithVersion = @{ Authorization = $inventory.Authorization; "If-Match" = "`"$($current.version)`"" }
$priceUpdate = Request PUT "/api/v1/inventory/vehicles/$($vehicle.id)" $headersWithVersion $newVehicle
Require 200 $priceUpdate "Inventario edita precio"
$forbiddenPublish = Request POST "/api/v1/inventory/vehicles/$($vehicle.id)/transitions" $inventory @{ toStatus = "Published"; reason = "Inventario intenta publicar" }
Require 403 $forbiddenPublish "Inventario intenta publicar"
$published = Request POST "/api/v1/inventory/vehicles/$($vehicle.id)/transitions" $manager @{ toStatus = "Published"; reason = "Gerente publica vehículo" }
Require 200 $published "Gerente publica vehículo"
Write-Output "Published: status=$((Json $published).status)"

$history = Invoke-RestMethod "$baseUrl/api/v1/inventory/vehicles/$($vehicle.id)/history" -Headers $manager
Write-Output "VehicleStatusHistory (roles resolved from seeded identities):"
$history.states | Sort-Object changedAt | ForEach-Object { [pscustomobject]@{ From = $_.fromStatus; To = $_.toStatus; ManualOverride = $_.manualOverride; Reason = $_.reason; Role = if ($_.changedById -eq $inventoryId) { "InventoryManager" } elseif ($_.changedById -eq $managerId) { "Manager" } else { "Unknown" } } } | Format-Table -AutoSize | Out-String | Write-Output
Write-Output "VehiclePriceHistory:"
$history.prices | Select-Object previousPrice, newPrice, currency, reason, changedById, changedAt | Format-Table -AutoSize | Out-String | Write-Output

$audit = Invoke-RestMethod "$baseUrl/api/v1/admin/audit" -Headers $manager
$priceAudit = $audit | Where-Object {
    if ($_.entityName -ne "Vehicle" -or $_.entityId -ne $vehicle.id -or [string]::IsNullOrWhiteSpace($_.oldValues)) { return $false }
    $oldValues = $_.oldValues | ConvertFrom-Json
    $newValues = $_.newValues | ConvertFrom-Json
    $oldValues.Price -ne $newValues.Price
} | Select-Object -First 1
if ($null -eq $priceAudit) { throw "No Vehicle price audit row was found for $($vehicle.id)." }
Write-Output "AuditLog price change:"
$priceAudit | Select-Object occurredAt, actorId, action, entityName, entityId, oldValues, newValues, correlationId | Format-List | Out-String | Write-Output

$equipment = Request POST "/api/v1/inventory/equipment" $manager @{ name = "Demo equipment $suffix" }
Require 201 $equipment "Equipment create"
$equipmentId = (Json $equipment).id
Require 204 (Request PUT "/api/v1/inventory/equipment/$equipmentId" $manager @{ name = "Demo equipment updated $suffix" }) "Equipment update"
Require 204 (Request DELETE "/api/v1/inventory/equipment/$equipmentId" $manager) "Equipment soft delete"
$technical = Request POST "/api/v1/inventory/technical-catalogs" $manager @{ name = "Demo technical $suffix"; category = "Fuel" }
Require 201 $technical "Technical catalog create"
$technicalId = (Json $technical).id
Require 204 (Request PUT "/api/v1/inventory/technical-catalogs/$technicalId" $manager @{ name = "Demo technical updated $suffix"; category = "Fuel" }) "Technical catalog update"
Require 204 (Request DELETE "/api/v1/inventory/technical-catalogs/$technicalId" $manager) "Technical catalog soft delete"
Write-Output "Phase 1 acceptance flow completed for vehicle $($vehicle.id)."
