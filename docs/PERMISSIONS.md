# Permissions

| Operation | Admin | Manager | SalesStaff | InventoryStaff |
|---|---|---|---|---|
| Product search, selling price, stock | yes | yes | yes | yes |
| Catalog edit / archive / categories / units | yes | yes | no | yes |
| Purchase costs, suppliers, purchases | yes | yes | no | yes (receive included) |
| Customers | yes | yes | yes | no |
| Sales / invoices / payments | all | all | own only | no |
| Stock adjustments / movements | yes | yes | no | yes |
| Physical sale reversal / refund | yes | yes | no | no |
| Business reports and audit | yes | yes | no | no |
| Users / role / active status | yes | no | no | no |
| Own profile | yes | yes | yes | yes |

API and MVC have independent route protection and shared service checks. Financial mutations reload active account and current roles **inside the same serialized transaction**. SalesStaff never receives product purchase costs or other staff invoices. One role per account. No public registration or public Admin assignment. Bootstrap is an explicit offline operator action. The last active administrator cannot be removed. Forwarded IP headers are not trusted; login limits use socket remote IP plus persistent Identity account lockout.
