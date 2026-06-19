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
            Fn("quote_get_project_summary", "Get a compact customer-safe summary of current project progress, estimate, blockers, and next actions.", new
            {
                type = "OBJECT",
                properties = new { }
            }),
            Fn("quote_get_connectors", "List customer-safe planned and future Make Studio connectors such as file import and CAD sender integrations.", new
            {
                type = "OBJECT",
                properties = new
                {
                    category = new { type = "STRING", description = "Optional connector category such as file_import or cad_sender." }
                }
            }),
            Fn("quote_get_connector_handoff", "Get a trusted customer-safe connector setup handoff for Make Studio integrations such as Google Drive.", new
            {
                type = "OBJECT",
                properties = new
                {
                    connector_id = new { type = "STRING", description = "Connector ID such as google-drive." },
                    return_url = new { type = "STRING", description = "Local return URL after trusted sign-in or connector setup completes." }
                },
                required = new[] { "connector_id" }
            }),
            Fn("quote_register_uploads", "Register uploaded or connector-provided manufacturing files into the current Make Studio session.", new
            {
                type = "OBJECT",
                properties = new
                {
                    requirements = new { type = "STRING", description = "Customer manufacturing request or notes to apply while interpreting the files." },
                    files = new
                    {
                        type = "ARRAY",
                        items = new
                        {
                            type = "OBJECT",
                            properties = new
                            {
                                file_name = new { type = "STRING", description = "Original file name." },
                                content_type = new { type = "STRING", description = "MIME content type." },
                                file_size_bytes = new { type = "INTEGER", description = "File size in bytes." },
                                kind = new { type = "STRING", description = "cad, drawing, photo, sketch, or supplemental." },
                                upload_id = new { type = "STRING", description = "Existing upload identifier when available." },
                                storage_path = new { type = "STRING", description = "Existing storage path when available." },
                                url = new { type = "STRING", description = "Browser-visible URL for image or PDF context when available." },
                                supersedes_part_id = new { type = "STRING", description = "Optional active QuoteEngine part UUID this corrected geometry replaces." },
                                supersedes_upload_id = new { type = "STRING", description = "Optional previous upload ID this corrected geometry replaces." },
                                supersedes_file_name = new { type = "STRING", description = "Optional previous file name this corrected geometry replaces." }
                            },
                            required = new[] { "file_name", "content_type", "file_size_bytes" }
                        }
                    },
                    supersedes_part_id = new { type = "STRING", description = "Optional active QuoteEngine part UUID replaced by this upload when only one corrected geometry file is provided." },
                    supersedes_upload_id = new { type = "STRING", description = "Optional previous upload ID replaced by this upload when only one corrected geometry file is provided." },
                    supersedes_file_name = new { type = "STRING", description = "Optional previous file name replaced by this upload when only one corrected geometry file is provided." }
                },
                required = new[] { "files" }
            }),
            Fn("quote_generate_3d_preview", "Generate an interactive 3D preview of a part you inferred from the customer's description, photo, sketch, or drawing so they can verify the shape and dimensions before providing a CAD file. Never ask for a CAD file as your first or only response — if you can infer shape and size, build the preview instead. Construct the part as an ordered cad_commands sequence: create primitives, position with translate, combine with cut/fuse, then apply fillet edge ops last.", new
            {
                type = "OBJECT",
                properties = new
                {
                    description = new { type = "STRING", description = "Short description of the inferred part, e.g. 'L-bracket 50x30mm with two M3 holes'." },
                    process_hint = new { type = "STRING", description = "Optional inferred process code such as fdm, sla, sls, or cnc." },
                    cad_commands = new
                    {
                        type = "ARRAY",
                        description = "Ordered build commands. Primitives: box params [w,d,h]; cylinder [radius,height]; sphere [radius]; cone [radiusBottom,radiusTop,height]. Boolean: cut/fuse with target_id+tool_id+result_id. Edge: fillet with target_id+radius+result_id. Move: translate with target_id+offset+result_id.",
                        items = new
                        {
                            type = "OBJECT",
                            properties = new
                            {
                                op = new { type = "STRING", description = "box, cylinder, sphere, cone, cut, fuse, fillet, translate, extrude, or revolve." },
                                id = new { type = "STRING", description = "Identifier for a primitive body." },
                                @params = new { type = "ARRAY", items = new { type = "NUMBER" }, description = "Op-specific numeric parameters in millimetres." },
                                target_id = new { type = "STRING", description = "Target body id for boolean, edge, or translate ops." },
                                tool_id = new { type = "STRING", description = "Tool body id for cut or fuse." },
                                result_id = new { type = "STRING", description = "Result body id produced by this op." },
                                radius = new { type = "NUMBER", description = "Fillet radius in millimetres." },
                                offset = new { type = "ARRAY", items = new { type = "NUMBER" }, description = "Translate offset [x,y,z] in millimetres." },
                                axis = new { type = "ARRAY", items = new { type = "NUMBER" }, description = "Revolve axis vector [x,y,z]." },
                                angle = new { type = "NUMBER", description = "Revolve angle in radians (2*pi is a full revolution)." },
                                profile = new
                                {
                                    type = "OBJECT",
                                    description = "Sketch profile for extrude or revolve.",
                                    properties = new
                                    {
                                        plane = new { type = "STRING", description = "Sketch plane such as XY." },
                                        segments = new
                                        {
                                            type = "ARRAY",
                                            description = "Ordered sketch segments forming a closed profile.",
                                            items = new
                                            {
                                                type = "OBJECT",
                                                properties = new
                                                {
                                                    type = new { type = "STRING", description = "Segment type such as line." },
                                                    @params = new { type = "ARRAY", items = new { type = "NUMBER" }, description = "Segment coordinates." }
                                                }
                                            }
                                        }
                                    }
                                }
                            },
                            required = new[] { "op" }
                        }
                    }
                },
                required = new[] { "description", "cad_commands" }
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
            Fn("quote_get_auth_handoff", "Get a customer-safe sign-in or sign-up handoff for checkout or quote actions without collecting credentials in chat.", new
            {
                type = "OBJECT",
                properties = new
                {
                    intent = new { type = "STRING", description = "Optional auth intent: sign-in or sign-up." },
                    return_url = new { type = "STRING", description = "Local return URL after trusted authentication completes." }
                }
            }),
            Fn("quote_get_settings", "Get customer-safe Make Studio settings for the current quote session, including language, units, currency, interaction mode, and artifact panel preference.", new
            {
                type = "OBJECT",
                properties = new { }
            }),
            Fn("quote_update_settings", "Update customer-safe Make Studio session settings such as language, units, currency, interaction mode, artifact panel preference, and multilingual mode.", new
            {
                type = "OBJECT",
                properties = new
                {
                    language = new { type = "STRING", description = "Preferred response language such as en or th." },
                    units = new { type = "STRING", description = "Preferred dimensional units: mm or inch." },
                    currency = new { type = "STRING", description = "Preferred quote currency such as THB or USD." },
                    interaction_mode = new { type = "STRING", description = "Preferred Make Studio interaction mode: chat, chat-and-ui, or ui." },
                    allow_artifact_panel = new { type = "BOOLEAN", description = "Whether the customer wants the artifact review panel available." },
                    multilingual = new { type = "BOOLEAN", description = "Whether bilingual/multilingual responses should remain enabled." }
                }
            }),
            Fn("quote_update_account_profile", "Prepare a confirmation-required update to the signed-in customer's safe account profile fields such as display name, phone, company, VAT number, language, currency, or timezone.", new
            {
                type = "OBJECT",
                properties = new
                {
                    // Snake_case only: the QuoteEngine BFF reads snake_case first
                    // (QuoteAgentService ReadString "display_name" ?? "displayName"), so declaring a
                    // single canonical casing keeps the schema clean without breaking the handler.
                    display_name = new { type = "STRING", description = "Customer display name to store after confirmation." },
                    phone = new { type = "STRING", description = "Customer phone number for account and checkout context." },
                    company_name = new { type = "STRING", description = "Customer company name, when applicable." },
                    vat_number = new { type = "STRING", description = "Customer VAT or tax registration number, when applicable." },
                    preferred_language = new { type = "STRING", description = "Preferred account language such as en or th." },
                    preferred_currency = new { type = "STRING", description = "Preferred quote currency such as THB or USD." },
                    timezone = new { type = "STRING", description = "Preferred customer timezone such as Asia/Bangkok." }
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
            Fn("quote_update_checkout_details", "Record billing, shipping, phone, company/VAT, terms, and consent details required before payment.", new
            {
                type = "OBJECT",
                properties = new
                {
                    billing_address_id = new { type = "STRING", description = "Customer billing address UUID." },
                    shipping_address_id = new { type = "STRING", description = "Customer shipping address UUID." },
                    phone = new { type = "STRING", description = "Checkout or delivery phone number." },
                    company = new { type = "STRING", description = "Billing company name when applicable." },
                    vat_number = new { type = "STRING", description = "Billing VAT or tax number when applicable." },
                    accepted_terms = new { type = "BOOLEAN", description = "Whether the customer accepted checkout terms." },
                    consent = new { type = "BOOLEAN", description = "Whether the customer granted required checkout consent." }
                },
                required = new[] { "billing_address_id", "shipping_address_id", "accepted_terms", "consent" }
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
            Fn("quote_pin_project", "Prepare pinning a customer Make Studio project for quick access. The BFF returns a confirmation card before changing project state.", new
            {
                type = "OBJECT",
                properties = new
                {
                    project_id = new { type = "STRING", description = "Optional project UUID. Omit to use the current draft project." }
                }
            }),
            Fn("quote_unpin_project", "Prepare unpinning a customer Make Studio project from quick access. The BFF returns a confirmation card before changing project state.", new
            {
                type = "OBJECT",
                properties = new
                {
                    project_id = new { type = "STRING", description = "Optional project UUID. Omit to use the current draft project." }
                }
            }),
            Fn("quote_archive_project", "Prepare archiving a customer Make Studio project. The BFF returns a confirmation card before changing project state.", new
            {
                type = "OBJECT",
                properties = new
                {
                    project_id = new { type = "STRING", description = "Optional project UUID. Omit to use the current draft project." }
                }
            }),
            Fn("quote_achieve_project", "Prepare marking a customer Make Studio project as achieved or completed. The BFF returns a confirmation card before changing project state.", new
            {
                type = "OBJECT",
                properties = new
                {
                    project_id = new { type = "STRING", description = "Optional project UUID. Omit to use the current draft project." }
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
            }),
            Fn("quote_set_ui_language", "Switch the Make Studio UI display language. Call this when the customer requests a UI language change or when you detect the customer is writing in a language that does not match the current UI language and wants the interface to match. The new culture is applied to the browser immediately after this turn. Supported values: en-US (English) or th-TH (Thai).", new
            {
                type = "OBJECT",
                properties = new
                {
                    culture = new { type = "STRING", description = "Target UI culture: en-US for English or th-TH for Thai." }
                },
                required = new[] { "culture" }
            }),
            Fn("quote_set_project_name", "Set a short descriptive project title derived from the part file name and inferred process or material. Call once per session after inferring manufacturing context. Never set the name to the customer's literal question.", new
            {
                type = "OBJECT",
                properties = new
                {
                    name = new { type = "STRING", description = "Short descriptive project title, e.g. 'Flower Oval – FDM PLA'." }
                },
                required = new[] { "name" }
            }),
            Fn("quote_focus_ui", "Highlight or focus a specific area of the Make Studio workspace to guide the customer to relevant information. Use when pointing the customer to an artifact, estimate, or configuration panel.", new
            {
                type = "OBJECT",
                properties = new
                {
                    panel = new { type = "STRING", description = "Target panel: artifact, estimate, config, or checkout." },
                    target_type = new { type = "STRING", description = "Target element type such as summary, part, or artifact." },
                    target_id = new { type = "STRING", description = "Optional target element ID." },
                    highlight_key = new { type = "STRING", description = "Highlight key for the focus directive." },
                    label = new { type = "STRING", description = "Customer-visible label describing what was highlighted." }
                }
            }),
            Fn("quote_ask_customer", "Present a focused clarifying question with 2–4 discrete answer options to the customer. Use ONLY when the customer's intent is genuinely ambiguous and the options are mutually exclusive (e.g. choosing between FDM, SLA, and SLS when the message gives no material hint). Do NOT use for open-ended questions, inferable information, quantity, lead time, or anything answerable from context. At most once per turn.", new
            {
                type = "OBJECT",
                properties = new
                {
                    question = new { type = "STRING", description = "The clarifying question to present to the customer." },
                    options = new
                    {
                        type = "ARRAY",
                        items = new { type = "STRING" },
                        description = "2 to 4 short answer options the customer can choose from."
                    }
                },
                required = new[] { "question", "options" }
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
