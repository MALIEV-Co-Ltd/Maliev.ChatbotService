using Maliev.ChatbotService.Application.Interfaces;

namespace Maliev.ChatbotService.Infrastructure.Tools;

/// <summary>
/// Central registry of all available tools with their Gemini function declarations.
/// </summary>
public static class ToolRegistry
{
    private const string QuoteEngineProfile = "quote-engine";
    private const string IntranetProfile = "intranet";

    /// <summary>
    /// Gets all tool declarations for Gemini function calling.
    /// </summary>
    public static List<GeminiToolDeclaration> GetAllToolDeclarations()
    {
        return Wrap(GetAllFunctionDeclarations());
    }

    /// <summary>
    /// Gets tool declarations for a channel-specific execution profile.
    /// </summary>
    /// <param name="profile">The execution profile name.</param>
    public static List<GeminiToolDeclaration> GetToolDeclarationsForProfile(string? profile)
    {
        return profile?.Trim().ToLowerInvariant() switch
        {
            IntranetProfile => GetAllToolDeclarations(),
            QuoteEngineProfile => Wrap(GetQuoteEngineFunctionDeclarations()),
            _ => []
        };
    }

    private static List<GeminiFunctionDeclaration> GetAllFunctionDeclarations()
    {
        var declarations = new List<GeminiFunctionDeclaration>();

        // Customer tools
        declarations.Add(Fn("search_customers", "Search for customers by name, email, phone, or company", new
        {
            type = "OBJECT",
            properties = new
            {
                query = new { type = "STRING", description = "Search term (name, email, phone, or company)" },
                page = new { type = "INTEGER", description = "Page number (default 1)" }
            },
            required = new[] { "query" }
        }));
        declarations.Add(Fn("get_customer", "Get detailed customer profile by ID", new
        {
            type = "OBJECT",
            properties = new { customer_id = new { type = "STRING", description = "Customer UUID" } },
            required = new[] { "customer_id" }
        }));
        declarations.Add(Fn("get_customer_metrics", "Get total count of customers", new
        {
            type = "OBJECT",
            properties = new { }
        }));
        declarations.Add(Fn("add_customer_address", "Add a shipping address to a customer", new
        {
            type = "OBJECT",
            properties = new
            {
                customer_id = new { type = "STRING", description = "Customer UUID" },
                street = new { type = "STRING", description = "Street address" },
                city = new { type = "STRING", description = "City" },
                state = new { type = "STRING", description = "State/Province" },
                postal_code = new { type = "STRING", description = "Postal Code" },
                country_id = new { type = "STRING", description = "Country UUID" },
                label = new { type = "STRING", description = "Address label (default: Shipping Address)" },
                is_default_shipping = new { type = "BOOLEAN", description = "Set as default shipping address" }
            },
            required = new[] { "customer_id", "street", "city", "country_id" }
        }));
        declarations.Add(Fn("list_customer_ndas", "List NDAs for a customer", new
        {
            type = "OBJECT",
            properties = new { customer_id = new { type = "STRING", description = "Customer UUID" } },
            required = new[] { "customer_id" }
        }));
        declarations.Add(Fn("get_document_content", "Get text content of a document (e.g. NDA) for summarization", new
        {
            type = "OBJECT",
            properties = new
            {
                document_id = new { type = "STRING", description = "Document UUID (from list_customer_ndas)" },
                file_reference = new { type = "STRING", description = "File reference ID (optional if document_id provided)" }
            }
        }));

        // Order tools
        declarations.Add(Fn("search_orders", "Search orders, optionally filtered by customer or status", new
        {
            type = "OBJECT",
            properties = new
            {
                customer_id = new { type = "STRING", description = "Filter by customer UUID" },
                status = new { type = "STRING", description = "Filter by status" },
                page = new { type = "INTEGER", description = "Page number (default 1)" }
            }
        }));
        declarations.Add(Fn("get_order", "Get order details by ID", new
        {
            type = "OBJECT",
            properties = new { order_id = new { type = "STRING", description = "Order UUID" } },
            required = new[] { "order_id" }
        }));

        // Quotation tools
        declarations.Add(Fn("search_quotations", "Search quotations, optionally filtered by customer or status", new
        {
            type = "OBJECT",
            properties = new
            {
                customer_id = new { type = "STRING", description = "Filter by customer UUID" },
                status = new { type = "STRING", description = "Filter by status" },
                page = new { type = "INTEGER", description = "Page number (default 1)" }
            }
        }));
        declarations.Add(Fn("get_quotation", "Get quotation details by ID", new
        {
            type = "OBJECT",
            properties = new { quotation_id = new { type = "STRING", description = "Quotation UUID" } },
            required = new[] { "quotation_id" }
        }));

        // Invoice tools
        declarations.Add(Fn("search_invoices", "Search invoices, optionally filtered by customer or status", new
        {
            type = "OBJECT",
            properties = new
            {
                customer_id = new { type = "STRING", description = "Filter by customer UUID" },
                status = new { type = "STRING", description = "Filter by status" },
                page = new { type = "INTEGER", description = "Page number (default 1)" }
            }
        }));
        declarations.Add(Fn("get_invoice", "Get invoice details by ID", new
        {
            type = "OBJECT",
            properties = new { invoice_id = new { type = "STRING", description = "Invoice UUID" } },
            required = new[] { "invoice_id" }
        }));

        // Payment tools
        declarations.Add(Fn("search_payments", "Search payments", new
        {
            type = "OBJECT",
            properties = new { page = new { type = "INTEGER", description = "Page number (default 1)" } }
        }));
        declarations.Add(Fn("get_payment", "Get payment details by ID", new
        {
            type = "OBJECT",
            properties = new { payment_id = new { type = "STRING", description = "Payment UUID" } },
            required = new[] { "payment_id" }
        }));

        // Employee tools
        declarations.Add(Fn("search_employees", "Search employees by name or department", new
        {
            type = "OBJECT",
            properties = new
            {
                query = new { type = "STRING", description = "Search term" },
                page = new { type = "INTEGER", description = "Page number (default 1)" }
            }
        }));
        declarations.Add(Fn("get_employee", "Get employee details by ID", new
        {
            type = "OBJECT",
            properties = new { employee_id = new { type = "STRING", description = "Employee UUID" } },
            required = new[] { "employee_id" }
        }));

        // Material tools
        declarations.Add(Fn("search_materials", "Search materials catalog", new
        {
            type = "OBJECT",
            properties = new
            {
                query = new { type = "STRING", description = "Search term" },
                page = new { type = "INTEGER", description = "Page number (default 1)" }
            }
        }));
        declarations.Add(Fn("get_material", "Get material details by ID", new
        {
            type = "OBJECT",
            properties = new { material_id = new { type = "STRING", description = "Material UUID" } },
            required = new[] { "material_id" }
        }));

        // Supplier tools
        declarations.Add(Fn("search_suppliers", "Search suppliers", new
        {
            type = "OBJECT",
            properties = new
            {
                query = new { type = "STRING", description = "Search term" },
                page = new { type = "INTEGER", description = "Page number (default 1)" }
            }
        }));
        declarations.Add(Fn("get_supplier", "Get supplier details by ID", new
        {
            type = "OBJECT",
            properties = new { supplier_id = new { type = "STRING", description = "Supplier UUID" } },
            required = new[] { "supplier_id" }
        }));

        // Receipt tools
        declarations.Add(Fn("search_receipts", "Search receipts", new
        {
            type = "OBJECT",
            properties = new { page = new { type = "INTEGER", description = "Page number (default 1)" } }
        }));
        declarations.Add(Fn("get_receipt", "Get receipt details by ID", new
        {
            type = "OBJECT",
            properties = new { receipt_id = new { type = "STRING", description = "Receipt UUID" } },
            required = new[] { "receipt_id" }
        }));

        return declarations;
    }

    private static List<GeminiFunctionDeclaration> GetQuoteEngineFunctionDeclarations()
    {
        return
        [
            Fn("quote_get_state", "Get the current QuoteEngine session state, gates, artifacts, attachments, and proposed actions.", new
            {
                type = "OBJECT",
                properties = new { }
            }),
            Fn("quote_resume_project", "Resume an existing customer-owned QuoteEngine project into the current Make Studio session.", new
            {
                type = "OBJECT",
                properties = new
                {
                    project_id = new { type = "STRING", description = "Project UUID to resume." }
                },
                required = new[] { "project_id" }
            }),
            Fn("quote_search_customer_data", "Search customer-owned Make Studio projects, quotes, orders, documents, files, and current-session artifacts.", new
            {
                type = "OBJECT",
                properties = new
                {
                    query = new { type = "STRING", description = "Optional search text. Omit or send empty string to list recent customer resources." },
                    limit = new { type = "INTEGER", description = "Maximum result count from 1 to 50." }
                }
            }),
            Fn("quote_get_reference_data", "Get customer-safe manufacturing reference options such as processes, materials, finishes, tolerances, quantities, and lead-time options.", new
            {
                type = "OBJECT",
                properties = new
                {
                    process = new { type = "STRING", description = "Optional manufacturing process code such as fdm, sla, sls, cnc, sheet-metal, or urethane-casting." }
                }
            }),
            Fn("quote_update_part_configuration", "Update draft quote configuration for process, material, finish or color, tolerance, quantity, and lead time. This is a draft-only action.", new
            {
                type = "OBJECT",
                properties = new
                {
                    part_id = new { type = "STRING", description = "Optional part identifier. Omit to apply to the primary part." },
                    process = new { type = "STRING", description = "Manufacturing process code." },
                    material = new { type = "STRING", description = "Material code or human-readable material." },
                    finish = new { type = "STRING", description = "Surface finish or post-processing option." },
                    color = new { type = "STRING", description = "Color selection if applicable." },
                    tolerance = new { type = "STRING", description = "Tolerance class or custom requirement." },
                    quantity = new { type = "INTEGER", description = "Requested production quantity." },
                    lead_time = new { type = "STRING", description = "Lead-time target or option." }
                }
            }),
            Fn("quote_calculate_estimate", "Request a current estimate after geometry and required configuration gates are satisfied.", new
            {
                type = "OBJECT",
                properties = new
                {
                    currency = new { type = "STRING", description = "Preferred currency code such as THB or USD." }
                }
            }),
            Fn("quote_prepare_draft_project", "Prepare a draft project action. The BFF returns a confirmation card before durable project creation.", new
            {
                type = "OBJECT",
                properties = new
                {
                    title = new { type = "STRING", description = "Draft project title." },
                    requirements = new { type = "STRING", description = "Project requirements summary." }
                }
            }),
            Fn("quote_duplicate_project", "Prepare duplication of the current customer draft project. The BFF returns a confirmation card before creating the duplicate.", new
            {
                type = "OBJECT",
                properties = new
                {
                    title = new { type = "STRING", description = "Optional title for the duplicated project." }
                }
            }),
            Fn("quote_prepare_formal_quote", "Prepare a formal quote action. The BFF returns a confirmation card and requires customer authentication before execution.", new
            {
                type = "OBJECT",
                properties = new
                {
                    requirements = new { type = "STRING", description = "Requirements summary to include in the formal quote artifact." }
                }
            }),
            Fn("quote_approve_quote", "Prepare quote approval after a formal quote is ready. The BFF returns a confirmation card before recording customer approval.", new
            {
                type = "OBJECT",
                properties = new
                {
                    note = new { type = "STRING", description = "Optional customer approval note." }
                }
            }),
            Fn("quote_acknowledge_dfm", "Prepare acknowledgement for reviewed DFM risks. The BFF returns a confirmation card before recording acknowledgement.", new
            {
                type = "OBJECT",
                properties = new
                {
                    issue_ids = new { type = "ARRAY", items = new { type = "STRING" }, description = "DFM issue identifiers that the customer reviewed." },
                    note = new { type = "STRING", description = "Customer acknowledgement note." }
                }
            }),
            Fn("quote_create_order", "Prepare manufacturing order creation. The BFF requires authentication, approved quote state, checkout readiness, and an explicit confirmation card.", new
            {
                type = "OBJECT",
                properties = new
                {
                    purchase_order = new { type = "STRING", description = "Optional customer PO number." },
                    requirements = new { type = "STRING", description = "Order requirements summary." }
                }
            }),
            Fn("quote_start_payment", "Prepare payment initiation. The BFF validates ownership, amount, terms, and checkout gates before returning a confirmation/payment handoff.", new
            {
                type = "OBJECT",
                properties = new
                {
                    order_number = new { type = "STRING", description = "Manufacturing order number." },
                    amount = new { type = "NUMBER", description = "Amount expected by the customer." },
                    currency = new { type = "STRING", description = "Currency code." }
                }
            }),
            Fn("quote_get_account_context", "Get customer sign-in state and safe account context for deciding whether to show sign-in, sign-up, or continuation prompts.", new
            {
                type = "OBJECT",
                properties = new { }
            })
        ];
    }

    private static List<GeminiToolDeclaration> Wrap(List<GeminiFunctionDeclaration> functionDeclarations)
    {
        return functionDeclarations.Count == 0
            ? []
            :
            [
                new()
                {
                    FunctionDeclarations = functionDeclarations
                }
            ];
    }

    private static GeminiFunctionDeclaration Fn(string name, string description, object parameters) =>
        new() { Name = name, Description = description, Parameters = parameters };
}
