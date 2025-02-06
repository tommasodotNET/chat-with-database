dab init --database-type "mssql" --connection-string "@env('ConnectionStrings__Agency')"

dab add "Offerings" --source "[dbo].[Offerings]" --permissions "anonymous:*"
dab add "OfferingDetails" --source "[dbo].[OfferingDetails]" --permissions "anonymous:*"

dab update Offerings --relationship offeringDetails --target.entity OfferingDetails --cardinality one
dab update OfferingDetails --relationship offerings --target.entity Offerings --cardinality many