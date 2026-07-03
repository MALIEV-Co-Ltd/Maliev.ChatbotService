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
            Fn("quote_get_state", "Read the full current session state: every workflow gate, the parts and their configuration, artifacts, attachments, the current estimate, and any pending confirmation actions. Read-only and safe to call anytime. Use when you need the detailed internal data (which gate blocks, exact part IDs); for anything you will summarize to the customer, prefer quote_get_project_summary.", new
            {
                type = "OBJECT",
                properties = new { }
            }),
            Fn("quote_get_project_summary", "Get a compact, customer-safe summary of the project: progress, current estimate, active blockers, and recommended next actions, plus - when an order exists - the order number, order status, payment status, and current or next manufacturing milestone. Read-only. Call to answer 'what's the status / where is my order / what's next', and to confirm readiness before formal quote, checkout, order, or payment. Prefer this over quote_get_state whenever you will summarize to the customer.", new
            {
                type = "OBJECT",
                properties = new { }
            }),
            Fn("quote_get_connectors", "List the customer-safe Make Studio connectors (file import, CAD sender, cloud storage such as Google Drive) with their availability and connect state. Read-only. Call first when a customer asks to connect or import from an integration, then use quote_get_connector_handoff for the one they chose. Never invent connector names or URLs.", new
            {
                type = "OBJECT",
                properties = new
                {
                    category = new { type = "STRING", description = "Optional connector category such as file_import or cad_sender." }
                }
            }),
            Fn("quote_get_connector_handoff", "Get a trusted, customer-safe setup handoff (a directive/URL the UI presents) for a specific connector such as Google Drive. Use after quote_get_connectors when the customer asks to connect that integration. Never invent or hard-code connector URLs; if the connector is already connected, do not ask the customer to authorize again.", new
            {
                type = "OBJECT",
                properties = new
                {
                    connector_id = new { type = "STRING", description = "Connector ID such as google-drive." },
                    return_url = new { type = "STRING", description = "Local return URL after trusted sign-in or connector setup completes." }
                },
                required = new[] { "connector_id" }
            }),
            Fn("quote_register_uploads", "Register the customer's uploaded or connector-imported manufacturing files (CAD, drawing, photo, sketch, or supplemental) into the current session so QuoteEngine can run geometry analysis and DFM. Call after files are attached or imported. For a corrected file sent after DFM feedback, set supersedes_part_id / supersedes_upload_id / supersedes_file_name so the fix replaces the old geometry and clears its stale DFM blockers instead of adding a second part.", new
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
            Fn("quote_cad_start_design", "Start a bounded iterative CAD design session for sketch/drawing/photo-derived preview work before applying operations. Use this when the customer has enough shape and scale context for a design attempt.", new
            {
                type = "OBJECT",
                properties = new
                {
                    description = new { type = "STRING", description = "Short manufacturing description of the target design." },
                    process_hint = new { type = "STRING", description = "Optional inferred process code such as fdm, sla, sls, or cnc." },
                    units = new { type = "STRING", description = "Dimensional units for the design, usually mm." }
                },
                required = new[] { "description" }
            }),
            Fn("quote_cad_apply_operations", "Apply one bounded CAD operation batch to an active design. Requires design_id and base_revision. The full design must stay at or below 80 CAD operations and 3 operation batches.", new
            {
                type = "OBJECT",
                properties = new
                {
                    design_id = new { type = "STRING", description = "CAD design session UUID returned by quote_cad_start_design." },
                    base_revision = new { type = "INTEGER", description = "Current design revision observed before this batch. Required to prevent stale edits." },
                    stage = new { type = "STRING", description = "Progress stage such as requirements, sketch_profile, construction_geometry, solid_features, detailing, or preview_finalized." },
                    operations = CadWorkbenchOperationsParameter()
                },
                required = new[] { "design_id", "base_revision", "operations" }
            }),
            Fn("quote_cad_observe_design", "Observe the current bounded CAD design revision, status, operation count, and remaining budget before deciding the next design action.", new
            {
                type = "OBJECT",
                properties = new
                {
                    design_id = new { type = "STRING", description = "Optional CAD design session UUID. Omit to observe the latest active design." }
                }
            }),
            Fn("quote_cad_finalize_preview", "Finalize an active bounded CAD design into the QuoteEngine generated 3D preview artifact after observing the latest revision.", new
            {
                type = "OBJECT",
                properties = new
                {
                    design_id = new { type = "STRING", description = "CAD design session UUID returned by quote_cad_start_design." },
                    base_revision = new { type = "INTEGER", description = "Current design revision observed before finalization. Required to prevent stale finalization." }
                },
                required = new[] { "design_id", "base_revision" }
            }),
            Fn("quote_generate_3d_preview", "Generate an interactive 3D preview only when the part shape and dimensions are explicit, readable from an attached drawing/PDF, CAD-derived, or confirmed by the customer. Do not use this tool for an unlabeled sketch or photo with no scale reference; ask for one focused dimension confirmation first. Never ask for a CAD file as your first or only response. Construct the part as an ordered cad_commands sequence: create primitives, position with translate, combine with cut/fuse, then apply fillet edge ops last.", new
            {
                type = "OBJECT",
                properties = new
                {
                    description = new { type = "STRING", description = "Short description of the inferred part, e.g. 'L-bracket 50x30mm with two M3 holes'." },
                    process_hint = new { type = "STRING", description = "Optional inferred process code such as fdm, sla, sls, or cnc." },
                    cad_commands = new
                    {
                        type = "ARRAY",
                        description = "Ordered build commands. Primitives: box params [w,d,h] and aliases plate/slot/cutout; cylinder [radius,height] and aliases hole/boss/standoff; sphere [radius]; cone [radiusBottom,radiusTop,height]. Boolean: cut/fuse/intersect with target_id+tool_id+result_id. Edge: fillet/chamfer with target_id+radius+result_id. Move: translate with target_id+offset+result_id; rotate with target_id+axis+angle. Outline shapes: reproduce the real silhouette instead of a bounding box - extrude with a profile of ordered move/line/arc segments (or a points/polyline list) params [height] traces a real outline (comb with teeth, letter, gear, bracket, hook); revolve sweeps a profile around an axis. Model repeated features explicitly: include every tooth in the extruded outline, or reuse one positioned cutter template with translate+cut for each hole/slot - a shape id may be reused across ops.",
                        items = new
                        {
                            type = "OBJECT",
                            properties = new
                            {
                                op = new { type = "STRING", description = "box, cylinder, sphere, cone, cut, fuse, intersect, fillet, chamfer, translate, rotate, extrude, revolve, loft, or accepted aliases such as plate, slot, hole, boss, or standoff." },
                                operation = new { type = "STRING", description = "Operation alias for op when the model uses operation." },
                                type = new { type = "STRING", description = "Operation type alias for op when the model uses type." },
                                shape = new { type = "STRING", description = "Shape alias for primitive op, such as box, cylinder, sphere, or cone." },
                                id = new { type = "STRING", description = "Identifier for a primitive body." },
                                name = new { type = "STRING", description = "Identifier alias normalized into id." },
                                shape_id = new { type = "STRING", description = "Shape identifier alias normalized into id." },
                                @params = new { type = "ARRAY", items = new { type = "NUMBER" }, description = "Op-specific numeric parameters in millimetres; QuoteEngine also accepts scalar numeric values and named objects." },
                                parameters = new { type = "ARRAY", items = new { type = "NUMBER" }, description = "Alias for params when the model emits parameters; scalar numeric values are accepted for single-value params." },
                                dimensions = new
                                {
                                    type = "OBJECT",
                                    description = "Named primitive dimensions normalized by QuoteEngine. Box accepts width/depth/length/height/thickness or x/y/z. Cylinder and sphere accept radius, diameter, or d. Cone accepts radiusBottom/radiusTop or bottomRadius/topRadius plus height.",
                                    properties = new
                                    {
                                        width = new { type = "NUMBER", description = "Box width in millimetres." },
                                        depth = new { type = "NUMBER", description = "Box depth in millimetres." },
                                        length = new { type = "NUMBER", description = "Box depth/length alias in millimetres." },
                                        height = new { type = "NUMBER", description = "Primitive height in millimetres." },
                                        thickness = new { type = "NUMBER", description = "Plate/sheet thickness alias for primitive height in millimetres." },
                                        x = new { type = "NUMBER", description = "Box X dimension alias in millimetres." },
                                        y = new { type = "NUMBER", description = "Box Y dimension alias in millimetres." },
                                        z = new { type = "NUMBER", description = "Box Z dimension alias in millimetres." },
                                        radius = new { type = "NUMBER", description = "Cylinder, sphere, or cone radius in millimetres." },
                                        diameter = new { type = "NUMBER", description = "Cylinder or sphere diameter in millimetres." },
                                        d = new { type = "NUMBER", description = "Short diameter alias in millimetres." },
                                        radiusBottom = new { type = "NUMBER", description = "Cone bottom radius in millimetres." },
                                        radiusTop = new { type = "NUMBER", description = "Cone top radius in millimetres." },
                                        bottomRadius = new { type = "NUMBER", description = "Cone bottom radius alias in millimetres." },
                                        topRadius = new { type = "NUMBER", description = "Cone top radius alias in millimetres." }
                                    }
                                },
                                width = new { type = "NUMBER", description = "Named box width in millimetres; normalized into params." },
                                depth = new { type = "NUMBER", description = "Named box depth in millimetres; normalized into params." },
                                length = new { type = "NUMBER", description = "Named box length/depth alias in millimetres; normalized into params." },
                                height = new { type = "NUMBER", description = "Named primitive height in millimetres; normalized into params." },
                                thickness = new { type = "NUMBER", description = "Plate/sheet thickness alias for box, cylinder, cone, or extrude height in millimetres." },
                                diameter = new { type = "NUMBER", description = "Cylinder or sphere diameter shorthand in millimetres." },
                                target_id = new { type = "STRING", description = "Target body id for boolean, edge, or translate ops." },
                                target = new { type = "STRING", description = "Target body id alias for boolean, edge, or translate ops." },
                                target_shape_id = new { type = "STRING", description = "Target shape identifier alias for boolean, edge, or transform ops." },
                                tool_id = new { type = "STRING", description = "Tool body id for cut or fuse." },
                                tool = new { type = "STRING", description = "Tool body id alias for cut or fuse." },
                                tool_shape_id = new { type = "STRING", description = "Tool shape identifier alias for cut or fuse." },
                                result_id = new { type = "STRING", description = "Result body id produced by this op." },
                                result = new { type = "STRING", description = "Result body id alias produced by this op." },
                                result_shape_id = new { type = "STRING", description = "Result shape identifier alias produced by this op." },
                                size = new { type = "ARRAY", items = new { type = "NUMBER" }, description = "Box size vector [width, depth, height] or profile square/rectangle size alias." },
                                radius = new { type = "NUMBER", description = "Fillet radius in millimetres." },
                                cornerRadius = new { type = "NUMBER", description = "Fillet or chamfer radius alias in millimetres." },
                                edgeRadius = new { type = "NUMBER", description = "Fillet or chamfer radius alias in millimetres." },
                                offset = new { type = "ARRAY", items = new { type = "NUMBER" }, description = "Translate offset [x,y,z] in millimetres." },
                                translation = new { type = "ARRAY", items = new { type = "NUMBER" }, description = "Translate offset alias [x,y,z] in millimetres." },
                                position = new { type = "ARRAY", items = new { type = "NUMBER" }, description = "Translate offset alias [x,y,z] in millimetres." },
                                location = new { type = "ARRAY", items = new { type = "NUMBER" }, description = "Translate offset alias [x,y,z] in millimetres." },
                                x = new { type = "NUMBER", description = "Translate X component in millimetres when offset is omitted." },
                                y = new { type = "NUMBER", description = "Translate Y component in millimetres when offset is omitted." },
                                z = new { type = "NUMBER", description = "Translate Z component in millimetres when offset is omitted." },
                                axis = new { type = "ARRAY", items = new { type = "NUMBER" }, description = "Revolve axis vector [x,y,z]." },
                                rotationAxis = new { type = "ARRAY", items = new { type = "NUMBER" }, description = "Rotate/revolve axis alias [x,y,z]." },
                                axisX = new { type = "NUMBER", description = "Axis X component for rotate or revolve when axis is omitted." },
                                axisY = new { type = "NUMBER", description = "Axis Y component for rotate or revolve when axis is omitted." },
                                axisZ = new { type = "NUMBER", description = "Axis Z component for rotate or revolve when axis is omitted." },
                                angle = new { type = "NUMBER", description = "Revolve angle in radians (2*pi is a full revolution)." },
                                angleDegrees = new { type = "NUMBER", description = "Rotate or revolve angle in degrees; normalized into radians." },
                                degrees = new { type = "NUMBER", description = "Short degree angle alias; normalized into radians." },
                                profile = new
                                {
                                    type = "OBJECT",
                                    description = "Sketch profile for extrude or revolve. Use rectangle/rect/square for rectangular profiles and circle/circular/round for circular profiles.",
                                    properties = new
                                    {
                                        plane = new { type = "STRING", description = "Sketch plane such as XY." },
                                        type = new { type = "STRING", description = "Profile shorthand such as rectangle, rect, square, circle, circular, or round." },
                                        @params = new { type = "ARRAY", items = new { type = "NUMBER" }, description = "Profile shorthand params, e.g. rectangle [width,height] or circle [radius]; scalar number accepted for circle radius or square size." },
                                        parameters = new { type = "ARRAY", items = new { type = "NUMBER" }, description = "Alias for profile params; scalar number accepted for single-value params." },
                                        size = new { type = "ARRAY", items = new { type = "NUMBER" }, description = "Rectangle size [width,height] or square size [side]." },
                                        width = new { type = "NUMBER", description = "Rectangle profile width." },
                                        height = new { type = "NUMBER", description = "Rectangle profile height." },
                                        radius = new { type = "NUMBER", description = "Circle profile radius." },
                                        diameter = new { type = "NUMBER", description = "Circle profile diameter alias." },
                                        points = new { type = "ARRAY", items = new { type = "ARRAY", items = new { type = "NUMBER" } }, description = "Point-list sketch alias normalized into move/line segments." },
                                        polyline = new { type = "ARRAY", items = new { type = "ARRAY", items = new { type = "NUMBER" } }, description = "Polyline sketch alias normalized into move/line segments." },
                                        vertices = new { type = "ARRAY", items = new { type = "ARRAY", items = new { type = "NUMBER" } }, description = "Vertex-list sketch alias normalized into move/line segments." },
                                        segments = new
                                        {
                                            type = "ARRAY",
                                            description = "Ordered sketch segments forming a closed profile.",
                                            items = new
                                            {
                                                type = "OBJECT",
                                                properties = new
                                                {
                                                    type = new { type = "STRING", description = "Segment type: move (start the outline at [x,y]), line (straight segment to [x,y]), hLine/vLine (horizontal/vertical delta), arc (three-point arc), or bezier. Order segments as one closed loop tracing the silhouette; the first segment should be move." },
                                                    @params = new { type = "ARRAY", items = new { type = "NUMBER" }, description = "Segment coordinates in millimetres: move/line [x,y]; hLine/vLine [delta]; arc/bezier [x1,y1,x2,y2]." },
                                                    parameters = new { type = "ARRAY", items = new { type = "NUMBER" }, description = "Alias for segment params." },
                                                    x = new { type = "NUMBER", description = "Named X coordinate for move/line segments." },
                                                    y = new { type = "NUMBER", description = "Named Y coordinate for move/line segments." },
                                                    dx = new { type = "NUMBER", description = "Named horizontal delta for hLine segments." },
                                                    dy = new { type = "NUMBER", description = "Named vertical delta for vLine segments." },
                                                    length = new { type = "NUMBER", description = "Length fallback for hLine/vLine segments." }
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
            Fn("quote_resume_project", "Load an existing customer-owned project into the current session so its parts, configuration, and artifacts become active. Requires project_id, which comes from quote_search_customer_data. Use when the customer asks to continue or open a specific past project; for a reorder, prefer quote_duplicate_project into a fresh session. Requires an authenticated customer.", new
            {
                type = "OBJECT",
                properties = new
                {
                    project_id = new { type = "STRING", description = "Project UUID to resume." }
                },
                required = new[] { "project_id" }
            }),
            Fn("quote_search_customer_data", "Search the signed-in customer's own Make Studio projects, quotes, orders, documents, files, and current-session artifacts; omit query (or send an empty string) to list their recent items. Use to find the project_id or order the customer refers to before resuming, duplicating, or answering a status question. Requires an authenticated customer and returns nothing useful when signed out.", new
            {
                type = "OBJECT",
                properties = new
                {
                    query = new { type = "STRING", description = "Optional search text. Omit or send empty string to list recent customer resources." },
                    limit = new { type = "INTEGER", description = "Maximum result count from 1 to 50." }
                }
            }),
            Fn("quote_get_auth_handoff", "Get a trusted, customer-safe sign-in or sign-up handoff (the UI presents it; never collect credentials in chat). Call before any authentication-gated step - formal quote, checkout, order, or payment - when quote_get_account_context shows the customer is not signed in. Present only the returned handoff and do not proceed with the gated action until they are authenticated.", new
            {
                type = "OBJECT",
                properties = new
                {
                    intent = new { type = "STRING", description = "Optional auth intent: sign-in or sign-up." },
                    return_url = new { type = "STRING", description = "Local return URL after trusted authentication completes." }
                }
            }),
            Fn("quote_get_settings", "Read the current session's customer-safe Make Studio preferences: response language, units (mm/inch), currency, interaction mode, artifact-panel preference, and multilingual mode. Read-only. Use before changing a setting, or when the customer asks what their current preferences are.", new
            {
                type = "OBJECT",
                properties = new { }
            }),
            Fn("quote_update_settings", "Update the customer's session preferences (response language, units, currency, interaction mode, artifact panel, multilingual). Applies immediately with no confirmation card. Send only the fields the customer asked to change. To switch the UI display language, use quote_set_ui_language instead.", new
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
            Fn("quote_update_account_profile", "Prepare an update to the signed-in customer's safe account profile fields (display name, phone, company, VAT number, preferred language/currency, timezone). Requires authentication; the BFF returns a confirmation card and only persists after the customer confirms. Use for durable account changes - for session-only preferences use quote_update_settings.", new
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
            Fn("quote_get_reference_data", "Get the valid manufacturing options QuoteEngine supports - processes, materials, finishes, tolerances, quantities, and lead-time choices - optionally scoped to one process. Read-only. Use before quote_update_part_configuration so you offer and set real option codes instead of guessing.", new
            {
                type = "OBJECT",
                properties = new
                {
                    process = new { type = "STRING", description = "Optional manufacturing process code such as fdm, sla, sls, cnc, sheet-metal, or urethane-casting." }
                }
            }),
            Fn("quote_update_part_configuration", "Set the draft quote configuration - process, material, finish/color, tolerance, quantity, lead time - for a part. Draft-only and applied without a confirmation card; omit part_id to target the primary part. Use quote_get_reference_data first to pick valid codes, and follow with quote_calculate_estimate to refresh pricing.", new
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
            Fn("quote_calculate_estimate", "Request a fresh price estimate for the current part and configuration. Call once geometry (uploaded CAD or a generated 3D preview) and required configuration are ready - in the SAME turn after generating a preview or changing configuration. Returns the estimate, or a customer-friendly blocker naming the missing input; if blocked, explain that next step rather than the internal reason, and never promise to estimate 'later'.", new
            {
                type = "OBJECT",
                properties = new
                {
                    currency = new { type = "STRING", description = "Preferred currency code such as THB or USD." }
                }
            }),
            Fn("quote_update_checkout_details", "Record the billing/shipping/phone/company/VAT/terms/consent needed before an order and payment. Get billing_address_id and shipping_address_id from quote_get_account_context and reuse the customer's saved defaults - do not ask them to retype details QuoteEngine already returned. Required before quote_create_order / quote_start_payment when checkout is incomplete.", new
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
            Fn("quote_prepare_draft_project", "Prepare durable creation of a customer draft project from the current session so it can be saved, resumed, and ordered later. Requires authentication; the BFF returns a confirmation card and only creates the project after the customer confirms.", new
            {
                type = "OBJECT",
                properties = new
                {
                    title = new { type = "STRING", description = "Draft project title." },
                    requirements = new { type = "STRING", description = "Project requirements summary." }
                }
            }),
            Fn("quote_duplicate_project", "Prepare a copy of the current customer draft project into a fresh Make Studio session. This is the correct path when the customer wants to reorder or rerun an existing job - duplicate first, then change quantities/materials/files there instead of mutating completed order history. Requires authentication; confirmation-gated.", new
            {
                type = "OBJECT",
                properties = new
                {
                    title = new { type = "STRING", description = "Optional title for the duplicated project." }
                }
            }),
            Fn("quote_pin_project", "Prepare pinning a customer project for quick access in Make Studio. project_id comes from quote_search_customer_data; omit it to use the current draft project. Confirmation-gated by the BFF.", new
            {
                type = "OBJECT",
                properties = new
                {
                    project_id = new { type = "STRING", description = "Optional project UUID. Omit to use the current draft project." }
                }
            }),
            Fn("quote_unpin_project", "Prepare unpinning a customer project from quick access. project_id comes from quote_search_customer_data; omit it to use the current draft project. Confirmation-gated by the BFF.", new
            {
                type = "OBJECT",
                properties = new
                {
                    project_id = new { type = "STRING", description = "Optional project UUID. Omit to use the current draft project." }
                }
            }),
            Fn("quote_archive_project", "Prepare archiving a customer project so it leaves the active project list. project_id comes from quote_search_customer_data; omit it to use the current draft project. Confirmation-gated by the BFF.", new
            {
                type = "OBJECT",
                properties = new
                {
                    project_id = new { type = "STRING", description = "Optional project UUID. Omit to use the current draft project." }
                }
            }),
            Fn("quote_request_employee_review", "Prepare a request for a MALIEV employee to review the current Make Studio project. Use this when the customer asks staff to check DFM risks, manufacturability, pricing assumptions, or a design concern before continuing. The BFF returns a confirmation card before routing the project to employee review.", new
            {
                type = "OBJECT",
                properties = new
                {
                    project_id = new { type = "STRING", description = "Optional project UUID. Omit to use the current draft project." },
                    note = new { type = "STRING", description = "Customer-visible review question or concern for the MALIEV employee." }
                }
            }),
            Fn("quote_prepare_formal_quote", "Prepare generation of the formal quote artifact the customer reviews before approving. Call only after current geometry, DFM, configuration, and pricing are ready. Requires authentication; the BFF returns a confirmation card and generates the artifact only after the customer confirms. First step of the finalization sequence: formal quote -> approve -> create order -> payment.", new
            {
                type = "OBJECT",
                properties = new
                {
                    requirements = new { type = "STRING", description = "Requirements summary to include in the formal quote artifact." }
                }
            }),
            Fn("quote_approve_quote", "Prepare recording the customer's approval of the formal quote, which unlocks order creation. Call only after the formal quote artifact exists and the customer has reviewed it. Confirmation-gated by the BFF.", new
            {
                type = "OBJECT",
                properties = new
                {
                    note = new { type = "STRING", description = "Optional customer approval note." }
                }
            }),
            Fn("quote_acknowledge_dfm", "Prepare recording that the customer reviewed and accepts specific DFM risks so those risks stop blocking the quote. issue_ids come from the DFM findings in quote_get_project_summary / quote_get_state. Use only after you have explained the actual risks and options in plain language and the customer chose to proceed. Confirmation-gated.", new
            {
                type = "OBJECT",
                properties = new
                {
                    issue_ids = new { type = "ARRAY", items = new { type = "STRING" }, description = "DFM issue identifiers that the customer reviewed." },
                    note = new { type = "STRING", description = "Customer acknowledgement note." }
                }
            }),
            Fn("quote_create_order", "Prepare creating the manufacturing order from an approved quote. The BFF requires authentication, an approved quote, and checkout readiness, and returns a confirmation card before creating anything. Call only after quote_approve_quote and complete checkout details; the resulting order_number feeds quote_start_payment.", new
            {
                type = "OBJECT",
                properties = new
                {
                    customer_po_number = new { type = "STRING", description = "Optional customer PO number." },
                    requirements = new { type = "STRING", description = "Order requirements summary." }
                }
            }),
            Fn("quote_start_payment", "Prepare initiating payment for an existing order. order_number comes from the quote_create_order result / quote_get_project_summary. The BFF validates ownership, amount, terms, and checkout gates, then returns a confirmation/payment handoff. Reuse the same checkout_attempt_id when retrying the same payment so PaymentService can deduplicate.", new
            {
                type = "OBJECT",
                properties = new
                {
                    order_number = new { type = "STRING", description = "Manufacturing order number." },
                    amount = new { type = "NUMBER", description = "Amount expected by the customer." },
                    currency = new { type = "STRING", description = "Currency code." },
                    checkout_attempt_id = new { type = "STRING", description = "Stable UUID for this payment attempt. Reuse the same value when retrying the same handoff so PaymentService idempotency can deduplicate safely." }
                }
            }),
            Fn("quote_get_account_context", "Read the customer's sign-in state and safe account context, including default billing/shipping addresses and profile details used at checkout. Read-only. Call first for any checkout/order/payment flow to decide whether a sign-in/sign-up handoff is needed and to reuse saved addresses (their IDs feed quote_update_checkout_details) instead of asking the customer to retype them.", new
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

    private static object CadWorkbenchOperationsParameter() => new
    {
        type = "ARRAY",
        description = "Ordered CAD operation batch. Prefer sketch/profile operations first, then extrude/revolve/loft, then cut/fuse, then fillet/chamfer last.",
        items = new
        {
            type = "OBJECT",
            properties = new
            {
                op = new { type = "STRING", description = "box, cylinder, sphere, cone, extrude, revolve, loft, cut, fuse, intersect, fillet, chamfer, translate, rotate, or accepted aliases such as plate, hole, boss, or standoff." },
                id = new { type = "STRING", description = "Identifier for a created primitive or profile-derived body." },
                target_id = new { type = "STRING", description = "Target body id for boolean, edge, or transform operations." },
                tool_id = new { type = "STRING", description = "Tool body id for cut, fuse, intersect, sweep, or loft operations." },
                result_id = new { type = "STRING", description = "Result body id produced by this operation." },
                @params = new { type = "ARRAY", items = new { type = "NUMBER" }, description = "Operation-specific numeric parameters in millimetres." },
                radius = new { type = "NUMBER", description = "Fillet, chamfer, cylinder, sphere, or profile radius in millimetres when applicable." },
                height = new { type = "NUMBER", description = "Primitive or extrusion height in millimetres." },
                thickness = new { type = "NUMBER", description = "Plate or sheet thickness in millimetres." },
                offset = new { type = "ARRAY", items = new { type = "NUMBER" }, description = "Translate offset [x,y,z] in millimetres." },
                axis = new { type = "ARRAY", items = new { type = "NUMBER" }, description = "Rotate or revolve axis [x,y,z]." },
                angleDegrees = new { type = "NUMBER", description = "Rotate or revolve angle in degrees." },
                profile = new
                {
                    type = "OBJECT",
                    description = "2D profile for extrude or revolve. Use closed move/line/arc segments for sketch-derived silhouettes.",
                    properties = new
                    {
                        plane = new { type = "STRING", description = "Sketch plane such as XY, XZ, or YZ." },
                        type = new { type = "STRING", description = "Profile shorthand such as rectangle, square, circle, or custom." },
                        width = new { type = "NUMBER", description = "Rectangle profile width." },
                        height = new { type = "NUMBER", description = "Rectangle profile height." },
                        radius = new { type = "NUMBER", description = "Circle profile radius." },
                        segments = new
                        {
                            type = "ARRAY",
                            description = "Ordered closed sketch segments.",
                            items = new
                            {
                                type = "OBJECT",
                                properties = new
                                {
                                    type = new { type = "STRING", description = "Segment type: move, line, hLine, vLine, arc, or bezier." },
                                    @params = new { type = "ARRAY", items = new { type = "NUMBER" }, description = "Segment coordinates or deltas." },
                                    x = new { type = "NUMBER", description = "Named X coordinate for move/line segments." },
                                    y = new { type = "NUMBER", description = "Named Y coordinate for move/line segments." },
                                    dx = new { type = "NUMBER", description = "Horizontal delta for hLine segments." },
                                    dy = new { type = "NUMBER", description = "Vertical delta for vLine segments." }
                                }
                            }
                        }
                    }
                }
            },
            required = new[] { "op" }
        }
    };

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
