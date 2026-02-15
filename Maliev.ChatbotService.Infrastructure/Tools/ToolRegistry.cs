using Maliev.ChatbotService.Application.Interfaces;

namespace Maliev.ChatbotService.Infrastructure.Tools;

/// <summary>
/// Central registry of all available tools with their Gemini function declarations.
/// </summary>
public static class ToolRegistry
{
    /// <summary>
    /// Gets all tool declarations for Gemini function calling.
    /// </summary>
    public static List<GeminiToolDeclaration> GetAllToolDeclarations()
    {
        return new List<GeminiToolDeclaration>
        {
            new()
            {
                FunctionDeclarations = GetAllFunctionDeclarations()
            }
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

    private static GeminiFunctionDeclaration Fn(string name, string description, object parameters) =>
        new() { Name = name, Description = description, Parameters = parameters };
}
