// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Bases query parsing and execution primitives.
//!
//! Milestone N lands this module in waves. N0-1 owns only the expression
//! language parser; `.base` YAML parsing, serialization, scanner indexing,
//! and execution arrive in later issues.

use std::{
    collections::{BTreeSet, HashMap, HashSet},
    fmt,
};

use serde::{Deserialize, Serialize};
use serde_json::{Map as JsonMap, Number as JsonNumber, Value as JsonValue};
use yaml_rust2::{
    Yaml,
    parser::{Event as YamlEvent, Parser as YamlParser},
    scanner::{Marker as YamlMarker, TScalarStyle},
};

use self::expr::{Callee, Expr, ExprKind, Lit, PropertyRef, Span, parse_expr};

pub mod dql;
pub mod engine;
pub mod eval;
pub mod expr;

#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub struct BaseFile {
    pub raw: String,
    pub filters: Option<FilterNode>,
    pub formulas: Vec<(String, Expr)>,
    pub properties: Vec<(String, PropertyConfig)>,
    pub summaries: Vec<(String, Expr)>,
    pub views: Vec<ViewDef>,
    pub preserved: PreservedYaml,
    pub spans: BaseSpans,
}

#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub enum FilterNode {
    Stmt(Expr),
    And(Vec<FilterNode>),
    Or(Vec<FilterNode>),
    Not(Vec<FilterNode>),
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct PropertyConfig {
    pub display_name: Option<String>,
    pub preserved: PreservedYaml,
}

#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub struct ViewDef {
    pub view_type: ViewType,
    pub name: String,
    pub limit: Option<u64>,
    pub filters: Option<FilterNode>,
    pub group_by: Option<GroupBy>,
    pub order: Vec<String>,
    pub summaries: Vec<(String, SummaryRef)>,
    pub source: RowSource,
    pub slate_state: Option<JsonValue>,
    pub preserved: PreservedYaml,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub enum ViewType {
    Table,
    List,
    Cards,
    Map,
    Other(String),
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub enum RowSource {
    Files,
    Tasks,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct GroupBy {
    pub property: PropertyRef,
    pub ascending: bool,
}

#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub enum SummaryRef {
    Builtin(String),
    Custom(String),
}

#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub struct SlateQuery {
    pub source: QuerySource,
    pub row_source: RowSource,
    pub filters: Option<FilterNode>,
    pub formulas: Vec<(String, Expr)>,
    pub custom_summaries: Vec<(String, Expr)>,
    pub group_by: Option<GroupBy>,
    pub sort: Vec<SortKey>,
    pub columns: Vec<ColumnSelection>,
    pub summaries: Vec<(String, SummaryRef)>,
    pub limit: Option<u64>,
    pub view: ViewSpec,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub enum QuerySource {
    All,
    Folder(String),
    Tag(String),
    Recent { days: u64 },
    Linked { from_path: String, depth: u8 },
    Unsupported(String),
}

#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub struct SortKey {
    pub expr: Expr,
    pub ascending: bool,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct ColumnSelection {
    pub id: String,
    pub display_name: Option<String>,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub enum ViewSpec {
    Table { fallback_from: Option<ViewType> },
    List { fallback_from: Option<ViewType> },
}

#[derive(Debug, Clone, PartialEq, Eq, Default, Serialize, Deserialize)]
pub struct PreservedYaml {
    pub regions: Vec<PreservedRegion>,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct PreservedRegion {
    pub span: Span,
    pub text: String,
}

#[derive(Debug, Clone, PartialEq, Eq, Default, Serialize, Deserialize)]
pub struct BaseSpans {
    pub top_level: Vec<NamedRegion>,
    pub filters: Vec<PreservedRegion>,
    pub formulas: Vec<NamedRegion>,
    pub properties: Vec<NamedRegion>,
    pub summaries: Vec<NamedRegion>,
    pub views: Vec<ViewSpans>,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct NamedRegion {
    pub name: String,
    pub region: PreservedRegion,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct ViewSpans {
    pub entry: PreservedRegion,
    pub keys: Vec<NamedRegion>,
    pub filters: Vec<PreservedRegion>,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct BaseWarning {
    pub kind: BaseWarningKind,
    pub message: String,
    pub span: Option<Span>,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum BaseWarningKind {
    ParseFailed,
    InvalidFilter,
    InvalidExpression,
    InvalidProperty,
    InvalidView,
    MissingViewName,
    DuplicateViewName,
    MissingViewType,
    InvalidViewSource,
    InvalidLimit,
    InvalidGroupBy,
    CircularFormula,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub enum BaseEdit {
    SetViewKey {
        view: usize,
        key: String,
        value: String,
    },
    AddView {
        yaml: String,
    },
    RemoveView {
        view: usize,
    },
    RenameView {
        view: usize,
        name: String,
    },
    RemoveViewKey {
        view: usize,
        key: String,
    },
    SetViewFilters {
        view: usize,
        yaml: String,
    },
    SetTopLevelFilters {
        yaml: String,
    },
    SetFormula {
        name: String,
        expression: String,
    },
    RemoveFormula {
        name: String,
    },
    SetDisplayName {
        property: String,
        display_name: Option<String>,
    },
    SetSummaryAssignment {
        view: usize,
        property: String,
        summary: Option<String>,
    },
    SetSlateState {
        view: usize,
        yaml: Option<String>,
    },
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub enum SerializeError {
    MissingSpan { target: String },
    WouldClobber { span: Span, reason: String },
    InvalidEdit { message: String },
}

impl fmt::Display for SerializeError {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            SerializeError::MissingSpan { target } => {
                write!(f, "missing source span for {target}")
            }
            SerializeError::WouldClobber { reason, .. } => write!(f, "{reason}"),
            SerializeError::InvalidEdit { message } => write!(f, "{message}"),
        }
    }
}

impl std::error::Error for SerializeError {}

pub fn parse_base(source: &str) -> (BaseFile, Vec<BaseWarning>) {
    let mut base = BaseFile {
        raw: source.to_string(),
        filters: None,
        formulas: Vec::new(),
        properties: Vec::new(),
        summaries: Vec::new(),
        views: Vec::new(),
        preserved: PreservedYaml::default(),
        spans: BaseSpans::default(),
    };
    let mut warnings = Vec::new();

    if source.trim().is_empty() {
        return (base, warnings);
    }

    let docs = match yaml_rust2::YamlLoader::load_from_str(source) {
        Ok(docs) => docs,
        Err(err) => {
            warnings.push(BaseWarning {
                kind: BaseWarningKind::ParseFailed,
                message: format!("YAML parse error: {err}"),
                span: None,
            });
            return (base, warnings);
        }
    };

    if docs.len() > 1 {
        warnings.push(BaseWarning {
            kind: BaseWarningKind::ParseFailed,
            message: "base file must be a single YAML document".to_string(),
            span: None,
        });
    }

    let Some(root) = docs.into_iter().next() else {
        return (base, warnings);
    };
    let Yaml::Hash(root) = root else {
        if !matches!(root, Yaml::Null | Yaml::BadValue) {
            warnings.push(BaseWarning {
                kind: BaseWarningKind::ParseFailed,
                message: format!(
                    "base root must be a YAML mapping; got {}",
                    yaml_type_name(&root)
                ),
                span: None,
            });
        }
        return (base, warnings);
    };

    let structural = structural_regions(source).unwrap_or_default();
    let top_level_regions = structural.top_level;
    let formula_regions = structural.formulas;
    let property_regions = structural.properties;
    let summary_regions = structural.summaries;
    base.spans.top_level = named_regions_from_map(&top_level_regions);
    base.spans.formulas = named_regions_from_map(&formula_regions);
    base.spans.properties = named_regions_from_map(&property_regions);
    base.spans.summaries = named_regions_from_map(&summary_regions);
    base.spans.filters = top_level_regions
        .get("filters")
        .map(|region| filter_node_regions_in_span(source, region.span))
        .unwrap_or_default();
    base.spans.views = structural.views;
    let mut formula_sources: HashMap<String, String> = HashMap::new();

    for (key_yaml, value) in root {
        let key = yaml_key_to_string(&key_yaml);
        match key.as_str() {
            "filters" => {
                let filter_regions = top_level_regions
                    .get("filters")
                    .map(|region| filter_node_regions_in_span(source, region.span))
                    .unwrap_or_default();
                let mut region_cursor = 0usize;
                base.filters = Some(parse_filter_node(
                    &value,
                    &filter_regions,
                    &mut region_cursor,
                    &mut warnings,
                ));
            }
            "formulas" => {
                parse_expr_map(
                    &value,
                    &formula_regions,
                    BaseWarningKind::InvalidExpression,
                    "formula",
                    &mut base.formulas,
                    &mut formula_sources,
                    &mut warnings,
                );
            }
            "properties" => {
                base.properties =
                    parse_properties(&value, &property_regions, source, &mut warnings);
            }
            "summaries" => {
                let mut ignored_sources = HashMap::new();
                parse_expr_map(
                    &value,
                    &summary_regions,
                    BaseWarningKind::InvalidExpression,
                    "summary",
                    &mut base.summaries,
                    &mut ignored_sources,
                    &mut warnings,
                );
            }
            "views" => {
                base.views = parse_views(
                    &value,
                    &base.spans.views,
                    source,
                    &base.summaries,
                    &mut warnings,
                );
            }
            _ => {
                base.preserved.regions.push(
                    top_level_regions
                        .get(&key)
                        .cloned()
                        .unwrap_or_else(|| preserved_region(source, 0, source.len())),
                );
            }
        }
    }

    mark_circular_formulas(&mut base.formulas, &formula_sources, &mut warnings);
    (base, warnings)
}

pub fn view_query(base: &BaseFile, view: usize) -> SlateQuery {
    let view = &base.views[view];
    let filters = match (base.filters.clone(), view.filters.clone()) {
        (Some(base_filter), Some(view_filter)) => {
            Some(FilterNode::And(vec![base_filter, view_filter]))
        }
        (Some(base_filter), None) => Some(base_filter),
        (None, Some(view_filter)) => Some(view_filter),
        (None, None) => None,
    };
    view_query_with_filters(base, view, filters)
}

pub fn view_edit_query(base: &BaseFile, view: usize) -> SlateQuery {
    let view = &base.views[view];
    view_query_with_filters(base, view, view.filters.clone())
}

fn view_query_with_filters(
    base: &BaseFile,
    view: &ViewDef,
    filters: Option<FilterNode>,
) -> SlateQuery {
    let display_names: HashMap<&str, &str> = base
        .properties
        .iter()
        .filter_map(|(id, config)| {
            config
                .display_name
                .as_deref()
                .map(|name| (id.as_str(), name))
        })
        .collect();
    let default_task_order = [
        "task.text".to_string(),
        "task.status".to_string(),
        "task.due".to_string(),
        "task.file".to_string(),
    ];
    let order = if view.order.is_empty() && view.source == RowSource::Tasks {
        default_task_order.as_slice()
    } else {
        view.order.as_slice()
    };
    let columns = order
        .iter()
        .map(|id| ColumnSelection {
            id: id.clone(),
            display_name: display_names
                .get(id.as_str())
                .map(|name| (*name).to_string()),
        })
        .collect();
    let view_spec = match &view.view_type {
        ViewType::Table => ViewSpec::Table {
            fallback_from: None,
        },
        ViewType::List => ViewSpec::List {
            fallback_from: None,
        },
        other => ViewSpec::Table {
            fallback_from: Some(other.clone()),
        },
    };

    SlateQuery {
        source: QuerySource::All,
        row_source: view.source.clone(),
        filters,
        formulas: base.formulas.clone(),
        custom_summaries: base.summaries.clone(),
        group_by: view.group_by.clone(),
        sort: slate_sort_keys(view.slate_state.as_ref()),
        columns,
        summaries: view.summaries.clone(),
        limit: view.limit,
        view: view_spec,
    }
}

fn slate_sort_keys(slate_state: Option<&JsonValue>) -> Vec<SortKey> {
    let Some(JsonValue::Object(state)) = slate_state else {
        return Vec::new();
    };
    let Some(JsonValue::Array(items)) = state.get("sort") else {
        return Vec::new();
    };
    items.iter().filter_map(slate_sort_key).collect()
}

fn slate_sort_key(value: &JsonValue) -> Option<SortKey> {
    let JsonValue::Object(entry) = value else {
        return None;
    };
    let source = entry
        .get("expr")
        .and_then(JsonValue::as_str)
        .or_else(|| entry.get("property").and_then(JsonValue::as_str))?;
    Some(SortKey {
        expr: parse_expr(source).ok()?,
        ascending: slate_sort_ascending(entry)?,
    })
}

fn slate_sort_ascending(entry: &JsonMap<String, JsonValue>) -> Option<bool> {
    if let Some(ascending) = entry.get("ascending").and_then(JsonValue::as_bool) {
        return Some(ascending);
    }
    match entry
        .get("direction")
        .and_then(JsonValue::as_str)
        .map(str::to_ascii_lowercase)
        .as_deref()
    {
        Some("desc" | "descending") => Some(false),
        Some("asc" | "ascending") => Some(true),
        _ => None,
    }
}

pub fn serialize_base(base: &BaseFile, edits: &[BaseEdit]) -> Result<String, SerializeError> {
    if edits.is_empty() {
        return Ok(base.raw.clone());
    }

    // Edits form an ordered slice: later operations must plan against the
    // document and spans produced by earlier operations. This matters for
    // dependent batches such as removing every child from a mapping or
    // removing one formula before inserting its replacement.
    let mut current = base.clone();
    for edit in edits {
        let mut splices = Vec::new();
        collect_edit_splices(&current, edit, &mut splices)?;
        let output = apply_splices(&current.raw, splices)?;
        let (next, warnings) = parse_base(&output);
        if let Some(warning) = warnings
            .iter()
            .find(|warning| warning.kind == BaseWarningKind::ParseFailed)
        {
            return Err(SerializeError::InvalidEdit {
                message: format!("edit produced invalid YAML: {}", warning.message),
            });
        }
        current = next;
    }
    Ok(current.raw)
}

#[derive(Debug)]
struct Splice {
    span: Span,
    replacement: String,
    order: usize,
}

fn collect_edit_splices(
    base: &BaseFile,
    edit: &BaseEdit,
    splices: &mut Vec<Splice>,
) -> Result<(), SerializeError> {
    match edit {
        BaseEdit::SetViewKey { view, key, value } => {
            ensure_view_key_is_editable(base, *view, key)?;
            ensure_set_view_key_is_closed(key)?;
            let scalar = editable_view_scalar(key, value);
            replace_or_insert_view_key_preserving_scalar(
                base,
                *view,
                key,
                scalar.as_deref(),
                &key_value_fragment(key, value),
                splices,
            )
        }
        BaseEdit::AddView { yaml } => push_add_view_splice(base, yaml, splices),
        BaseEdit::RemoveView { view } => push_remove_view_splice(base, *view, splices),
        BaseEdit::RenameView { view, name } => replace_or_insert_view_key_preserving_scalar(
            base,
            *view,
            "name",
            Some(name),
            &format!("name: {}", quote_yaml_string(name)),
            splices,
        ),
        BaseEdit::RemoveViewKey { view, key } => {
            ensure_view_key_is_editable(base, *view, key)?;
            ensure_remove_view_key_is_closed(key)?;
            push_remove_view_key_splice(base, *view, key, splices)
        }
        BaseEdit::SetViewFilters { view, yaml } => replace_or_insert_view_key(
            base,
            *view,
            "filters",
            &key_value_fragment("filters", yaml),
            splices,
        ),
        BaseEdit::SetTopLevelFilters { yaml } => replace_or_insert_top_level(
            base,
            "filters",
            &key_value_fragment("filters", yaml),
            splices,
        ),
        BaseEdit::SetFormula { name, expression } => {
            replace_or_insert_formula(base, name, expression, splices)
        }
        BaseEdit::RemoveFormula { name } => push_remove_formula_splice(base, name, splices),
        BaseEdit::SetDisplayName {
            property,
            display_name,
        } => replace_or_remove_display_name(base, property, display_name.as_deref(), splices),
        BaseEdit::SetSummaryAssignment {
            view,
            property,
            summary,
        } => {
            replace_or_remove_summary_assignment(base, *view, property, summary.as_deref(), splices)
        }
        BaseEdit::SetSlateState { view, yaml } => {
            replace_or_remove_slate_state(base, *view, yaml.as_deref(), splices)
        }
    }
}

fn replace_or_insert_top_level(
    base: &BaseFile,
    key: &str,
    fragment: &str,
    splices: &mut Vec<Splice>,
) -> Result<(), SerializeError> {
    if let Some(region) = named_region(&base.spans.top_level, key) {
        let replacement = format_fragment_for_region(&region.region, fragment);
        push_splice(splices, region.region.span, replacement);
        return Ok(());
    }

    if push_root_flow_mapping_entry(base, fragment, splices)? {
        return Ok(());
    }

    let offset = named_region(&base.spans.top_level, "views")
        .map(|region| region.region.span.start)
        .unwrap_or(base.raw.len() as u32);
    push_insertion_splice(
        splices,
        &base.raw,
        offset,
        format_yaml_fragment(fragment, "", ""),
    );
    Ok(())
}

fn replace_or_insert_formula(
    base: &BaseFile,
    name: &str,
    expression: &str,
    splices: &mut Vec<Splice>,
) -> Result<(), SerializeError> {
    let entry_fragment = format!("{}: {}", yaml_key(name), quote_yaml_string(expression));
    if let Some(region) = named_region(&base.spans.formulas, name) {
        if !push_scalar_replacement(splices, &region.region, expression) {
            push_splice(
                splices,
                region.region.span,
                format_fragment_for_region(&region.region, &entry_fragment),
            );
        }
        return Ok(());
    }

    if let Some(formulas) = named_region(&base.spans.top_level, "formulas") {
        if empty_flow_collection(&formulas.region, "{}") {
            push_splice(
                splices,
                formulas.region.span,
                expand_empty_collection_region(&formulas.region, "formulas", &entry_fragment),
            );
            return Ok(());
        }
        if let Some((offset, has_trailing_comma, continuation_padding)) =
            flow_collection_append_point(&formulas.region, "formulas", FlowCollectionKind::Mapping)
        {
            let delimiter = if has_trailing_comma { "" } else { ", " };
            let padding = " ".repeat(continuation_padding);
            push_splice(
                splices,
                Span {
                    start: offset,
                    end: offset,
                },
                format!("{padding}{delimiter}{entry_fragment}"),
            );
            return Ok(());
        }
        let child_indent = child_mapping_indent(&formulas.region)
            .ok_or_else(|| missing_span("formulas child indentation"))?;
        let entry = format!("{}{entry_fragment}\n", " ".repeat(child_indent));
        push_insertion_splice(splices, &base.raw, formulas.region.span.end, entry);
        return Ok(());
    }

    let section = format!("formulas:\n  {entry_fragment}\n");
    if push_root_flow_mapping_entry(base, &section, splices)? {
        return Ok(());
    }
    let offset = named_region(&base.spans.top_level, "views")
        .map(|region| region.region.span.start)
        .unwrap_or(base.raw.len() as u32);
    push_insertion_splice(splices, &base.raw, offset, section);
    Ok(())
}

fn push_remove_formula_splice(
    base: &BaseFile,
    name: &str,
    splices: &mut Vec<Splice>,
) -> Result<(), SerializeError> {
    let region = named_region(&base.spans.formulas, name)
        .ok_or_else(|| missing_span(format!("formula {name:?}")))?;
    let formulas =
        named_region(&base.spans.top_level, "formulas").ok_or_else(|| missing_span("formulas"))?;
    if flow_collection_close(&formulas.region, "formulas", FlowCollectionKind::Mapping).is_some() {
        let index = base
            .spans
            .formulas
            .iter()
            .position(|candidate| candidate.region.span == region.region.span)
            .ok_or_else(|| missing_span(format!("formula {name:?}")))?;
        let spans = base
            .spans
            .formulas
            .iter()
            .map(|formula| formula.region.span)
            .collect::<Vec<_>>();
        return push_flow_item_removal(&base.raw, &spans, index, splices);
    }
    if base.spans.formulas.len() == 1 {
        push_block_parent_collapse(
            splices,
            &base.raw,
            &formulas.region,
            "formulas",
            "{}",
            region.region.span,
        )?;
        return Ok(());
    }
    push_block_removal_splice(splices, &base.raw, region.region.span);
    Ok(())
}

fn replace_or_remove_display_name(
    base: &BaseFile,
    property: &str,
    display_name: Option<&str>,
    splices: &mut Vec<Splice>,
) -> Result<(), SerializeError> {
    let Some(property_region) = named_region(&base.spans.properties, property) else {
        if let Some(name) = display_name {
            if let Some(properties) = named_region(&base.spans.top_level, "properties") {
                if let Some(root) = source_document(&base.raw)
                    && let Some(properties_node) = mapping_value(&root, "properties")
                    && let SourceNodeKind::Mapping {
                        flow: true,
                        entries,
                    } = &properties_node.kind
                {
                    let close = properties_node
                        .end
                        .checked_sub(1)
                        .ok_or_else(|| missing_span("properties flow close"))?;
                    push_splice(
                        splices,
                        Span {
                            start: close as u32,
                            end: close as u32,
                        },
                        format!(
                            "{}{}: {{ displayName: {} }}",
                            if entries.is_empty() { "" } else { ", " },
                            yaml_key(property),
                            quote_yaml_string(name)
                        ),
                    );
                    return Ok(());
                }
                let property_indent = child_mapping_indent(&properties.region)
                    .or_else(|| {
                        properties
                            .region
                            .text
                            .lines()
                            .next()
                            .and_then(key_on_line)
                            .map(|(_, indent)| indent + 2)
                    })
                    .ok_or_else(|| missing_span("properties child indentation"))?;
                let entry = format!(
                    "{}{}:\n{}displayName: {}\n",
                    " ".repeat(property_indent),
                    yaml_key(property),
                    " ".repeat(property_indent + 2),
                    quote_yaml_string(name)
                );
                push_insertion_splice(splices, &base.raw, properties.region.span.end, entry);
            } else {
                let section = format!(
                    "properties:\n  {}:\n    displayName: {}\n",
                    yaml_key(property),
                    quote_yaml_string(name)
                );
                let offset = named_region(&base.spans.top_level, "views")
                    .map(|region| region.region.span.start)
                    .unwrap_or(base.raw.len() as u32);
                push_insertion_splice(splices, &base.raw, offset, section);
            }
        }
        return Ok(());
    };
    if let Some(root) = source_document(&base.raw)
        && let Some(properties) = mapping_value(&root, "properties")
        && let Some(property_node) = mapping_value(properties, property)
        && let SourceNodeKind::Mapping {
            flow: true,
            entries,
        } = &property_node.kind
    {
        let child_regions = mapping_regions(&base.raw, property_node);
        if let Some(region) = child_regions.get("displayName") {
            if let Some(name) = display_name {
                if !push_scalar_replacement(splices, region, name) {
                    push_splice(
                        splices,
                        region.span,
                        format!("displayName: {}", quote_yaml_string(name)),
                    );
                }
            } else {
                let spans = entries
                    .iter()
                    .map(|entry| Span {
                        start: entry.key.start as u32,
                        end: entry.value.end as u32,
                    })
                    .collect::<Vec<_>>();
                let index = entries
                    .iter()
                    .position(|entry| {
                        matches!(
                            &entry.key.kind,
                            SourceNodeKind::Scalar { value } if value == "displayName"
                        )
                    })
                    .ok_or_else(|| missing_span("flow displayName entry"))?;
                push_flow_item_removal(&base.raw, &spans, index, splices)?;
            }
        } else if let Some(name) = display_name {
            let close = property_node
                .end
                .checked_sub(1)
                .ok_or_else(|| missing_span(format!("property {property:?} flow close")))?;
            push_splice(
                splices,
                Span {
                    start: close as u32,
                    end: close as u32,
                },
                format!(
                    "{}displayName: {}",
                    if entries.is_empty() { "" } else { ", " },
                    quote_yaml_string(name)
                ),
            );
        }
        return Ok(());
    }
    let child_indent = child_mapping_indent(&property_region.region);
    let child_regions = child_indent
        .map(|indent| regions_in_span(&base.raw, property_region.region.span, indent))
        .unwrap_or_default();

    match (display_name, child_regions.get("displayName")) {
        (Some(name), Some(region)) => {
            if !push_scalar_replacement(splices, region, name) {
                push_splice(
                    splices,
                    region.span,
                    format_fragment_for_region(
                        region,
                        &format!("displayName: {}", quote_yaml_string(name)),
                    ),
                );
            }
        }
        (Some(name), None) => {
            let child_indent = child_indent
                .ok_or_else(|| missing_span(format!("property {property:?} child indentation")))?;
            let prefix = " ".repeat(child_indent);
            push_insertion_splice(
                splices,
                &base.raw,
                property_region.region.span.end,
                format_yaml_fragment(
                    &format!("displayName: {}", quote_yaml_string(name)),
                    &prefix,
                    &prefix,
                ),
            );
        }
        (None, Some(region)) => {
            if child_regions.len() == 1 {
                push_block_parent_collapse(
                    splices,
                    &base.raw,
                    &property_region.region,
                    property,
                    "{}",
                    region.span,
                )?;
            } else {
                push_block_removal_splice(splices, &base.raw, region.span);
            }
        }
        (None, None) => {}
    }
    Ok(())
}

fn replace_or_remove_summary_assignment(
    base: &BaseFile,
    view: usize,
    property: &str,
    summary: Option<&str>,
    splices: &mut Vec<Splice>,
) -> Result<(), SerializeError> {
    let view_spans = view_spans_for(base, view)?;
    let Some(summaries_region) = named_region(&view_spans.keys, "summaries") else {
        if let Some(summary) = summary {
            let fragment = format!(
                "summaries:\n  {}: {}",
                yaml_key(property),
                quote_yaml_string(summary)
            );
            push_view_key_insertion(base, view, view_spans, &fragment, splices)?;
        }
        return Ok(());
    };

    if let Some(root) = source_document(&base.raw)
        && let Some(views) = mapping_value(&root, "views")
        && let SourceNodeKind::Sequence { items, .. } = &views.kind
        && let Some(view_node) = items.get(view)
        && let Some(summaries_node) = mapping_value(view_node, "summaries")
        && let SourceNodeKind::Mapping {
            flow: true,
            entries,
        } = &summaries_node.kind
    {
        let assignments = mapping_regions(&base.raw, summaries_node);
        if let Some(region) = assignments.get(property) {
            if let Some(summary) = summary {
                if !push_scalar_replacement(splices, region, summary) {
                    push_splice(
                        splices,
                        region.span,
                        format!("{}: {}", yaml_key(property), quote_yaml_string(summary)),
                    );
                }
            } else {
                let spans = entries
                    .iter()
                    .map(|entry| Span {
                        start: entry.key.start as u32,
                        end: entry.value.end as u32,
                    })
                    .collect::<Vec<_>>();
                let index = entries
                    .iter()
                    .position(|entry| {
                        matches!(
                            &entry.key.kind,
                            SourceNodeKind::Scalar { value } if value == property
                        )
                    })
                    .ok_or_else(|| missing_span(format!("summary assignment {property:?}")))?;
                push_flow_item_removal(&base.raw, &spans, index, splices)?;
            }
        } else if let Some(summary) = summary {
            let close = summaries_node
                .end
                .checked_sub(1)
                .ok_or_else(|| missing_span("flow summaries close"))?;
            push_splice(
                splices,
                Span {
                    start: close as u32,
                    end: close as u32,
                },
                format!(
                    "{}{}: {}",
                    if entries.is_empty() { "" } else { ", " },
                    yaml_key(property),
                    quote_yaml_string(summary)
                ),
            );
        }
        return Ok(());
    }

    let assignment_indent = child_mapping_indent(&summaries_region.region)
        .or_else(|| {
            summaries_region
                .region
                .text
                .lines()
                .next()
                .and_then(key_on_line)
                .map(|(_, indent)| indent + 2)
        })
        .ok_or_else(|| missing_span(format!("view {view} summaries child indentation")))?;
    let assignment_prefix = " ".repeat(assignment_indent);
    let assignments = regions_in_span(&base.raw, summaries_region.region.span, assignment_indent);
    if summary.is_none() && assignments.len() == 1 && assignments.contains_key(property) {
        push_block_parent_collapse(
            splices,
            &base.raw,
            &summaries_region.region,
            "summaries",
            "{}",
            assignments[property].span,
        )?;
        return Ok(());
    }
    match (summary, assignments.get(property)) {
        (Some(summary), Some(region)) => {
            if !push_scalar_replacement(splices, region, summary) {
                push_splice(
                    splices,
                    region.span,
                    format!(
                        "{assignment_prefix}{}: {}\n",
                        yaml_key(property),
                        quote_yaml_string(summary)
                    ),
                );
            }
        }
        (Some(summary), None) => {
            push_insertion_splice(
                splices,
                &base.raw,
                summaries_region.region.span.end,
                format!(
                    "{assignment_prefix}{}: {}\n",
                    yaml_key(property),
                    quote_yaml_string(summary)
                ),
            );
        }
        (None, Some(region)) => {
            push_block_removal_splice(splices, &base.raw, region.span);
        }
        (None, None) => {}
    }
    Ok(())
}

fn replace_or_remove_slate_state(
    base: &BaseFile,
    view: usize,
    yaml: Option<&str>,
    splices: &mut Vec<Splice>,
) -> Result<(), SerializeError> {
    let view_spans = view_spans_for(base, view)?;
    if let Some(root) = source_document(&base.raw)
        && let Some(views) = mapping_value(&root, "views")
        && let SourceNodeKind::Sequence { items, .. } = &views.kind
        && let Some(view_node) = items.get(view)
        && let SourceNodeKind::Mapping {
            flow: true,
            entries,
        } = &view_node.kind
    {
        if let Some(region) = named_region(&view_spans.keys, "slate") {
            if let Some(yaml) = yaml {
                push_splice(
                    splices,
                    region.region.span,
                    format!("slate: {}", flow_edit_value("slate", yaml)?),
                );
            } else {
                let spans = entries
                    .iter()
                    .map(|entry| Span {
                        start: entry.key.start as u32,
                        end: entry.value.end as u32,
                    })
                    .collect::<Vec<_>>();
                let index = entries
                    .iter()
                    .position(|entry| {
                        matches!(
                            &entry.key.kind,
                            SourceNodeKind::Scalar { value } if value == "slate"
                        )
                    })
                    .ok_or_else(|| missing_span("flow slate entry"))?;
                push_flow_item_removal(&base.raw, &spans, index, splices)?;
            }
        } else if let Some(yaml) = yaml {
            let close = view_node
                .end
                .checked_sub(1)
                .ok_or_else(|| missing_span(format!("view {view} flow close")))?;
            push_splice(
                splices,
                Span {
                    start: close as u32,
                    end: close as u32,
                },
                format!(
                    "{}slate: {}",
                    if entries.is_empty() { "" } else { ", " },
                    flow_edit_value("slate", yaml)?
                ),
            );
        }
        return Ok(());
    }

    match yaml {
        Some(yaml) => replace_or_insert_view_key(
            base,
            view,
            "slate",
            &key_value_fragment("slate", yaml),
            splices,
        ),
        None => {
            if let Some(region) = named_region(&view_spans.keys, "slate") {
                push_block_removal_splice(splices, &base.raw, region.region.span);
            }
            Ok(())
        }
    }
}

fn flow_edit_value(key: &str, yaml: &str) -> Result<String, SerializeError> {
    let fragment = key_value_fragment(key, yaml);
    let documents = yaml_rust2::YamlLoader::load_from_str(&fragment).map_err(|error| {
        SerializeError::InvalidEdit {
            message: format!("{key} YAML is invalid: {error}"),
        }
    })?;
    let Some(Yaml::Hash(mapping)) = documents.first() else {
        return Err(SerializeError::InvalidEdit {
            message: format!("{key} YAML must be a mapping fragment"),
        });
    };
    let value = mapping
        .iter()
        .find_map(|(candidate, value)| (yaml_key_to_string(candidate) == key).then_some(value))
        .ok_or_else(|| SerializeError::InvalidEdit {
            message: format!("{key} YAML must contain {key:?}"),
        })?;
    render_flow_yaml(value)
}

fn replace_or_insert_view_key(
    base: &BaseFile,
    view: usize,
    key: &str,
    fragment: &str,
    splices: &mut Vec<Splice>,
) -> Result<(), SerializeError> {
    let view_spans = view_spans_for(base, view)?;
    if let Some(region) = named_region(&view_spans.keys, key) {
        let replacement = if view_uses_flow_mapping(&base.raw, view) {
            flow_mapping_entry(fragment)?
        } else {
            format_fragment_for_region(&region.region, fragment)
        };
        push_splice(splices, region.region.span, replacement);
        return Ok(());
    }

    push_view_key_insertion(base, view, view_spans, fragment, splices)
}

fn view_uses_flow_mapping(source: &str, view: usize) -> bool {
    let Some(root) = source_document(source) else {
        return false;
    };
    let Some(views) = mapping_value(&root, "views") else {
        return false;
    };
    let SourceNodeKind::Sequence { items, .. } = &views.kind else {
        return false;
    };
    items
        .get(view)
        .is_some_and(|view| matches!(&view.kind, SourceNodeKind::Mapping { flow: true, .. }))
}

fn root_uses_flow_mapping(source: &str) -> bool {
    source_document(source)
        .is_some_and(|root| matches!(root.kind, SourceNodeKind::Mapping { flow: true, .. }))
}

fn push_root_flow_mapping_entry(
    base: &BaseFile,
    fragment: &str,
    splices: &mut Vec<Splice>,
) -> Result<bool, SerializeError> {
    let Some(root) = source_document(&base.raw) else {
        return Ok(false);
    };
    let SourceNodeKind::Mapping {
        flow: true,
        entries,
    } = &root.kind
    else {
        return Ok(false);
    };
    let close = root
        .end
        .checked_sub(1)
        .filter(|close| base.raw.as_bytes().get(*close) == Some(&b'}'))
        .ok_or_else(|| missing_span("root flow mapping close"))?;
    let offset = u32::try_from(close).map_err(|_| missing_span("root flow mapping close"))?;
    let has_trailing_comma = entries
        .last()
        .is_some_and(|entry| flow_suffix_has_comma(&base.raw, entry.value.end, close));
    let delimiter = if entries.is_empty() || has_trailing_comma {
        ""
    } else {
        ", "
    };
    push_splice(
        splices,
        Span {
            start: offset,
            end: offset,
        },
        format!("{delimiter}{}", flow_mapping_entry(fragment)?),
    );
    Ok(true)
}

fn flow_suffix_has_comma(source: &str, start: usize, end: usize) -> bool {
    let Some(bytes) = source.as_bytes().get(start..end) else {
        return false;
    };
    let mut in_comment = false;
    for &byte in bytes {
        if in_comment {
            if matches!(byte, b'\r' | b'\n') {
                in_comment = false;
            }
            continue;
        }
        match byte {
            b'#' => in_comment = true,
            b',' => return true,
            _ => {}
        }
    }
    false
}

fn replace_or_insert_view_key_preserving_scalar(
    base: &BaseFile,
    view: usize,
    key: &str,
    scalar: Option<&str>,
    fragment: &str,
    splices: &mut Vec<Splice>,
) -> Result<(), SerializeError> {
    let view_spans = view_spans_for(base, view)?;
    if let (Some(region), Some(value)) = (named_region(&view_spans.keys, key), scalar)
        && push_scalar_replacement(splices, &region.region, value)
    {
        return Ok(());
    }
    replace_or_insert_view_key(base, view, key, fragment, splices)
}

fn push_add_view_splice(
    base: &BaseFile,
    yaml: &str,
    splices: &mut Vec<Splice>,
) -> Result<(), SerializeError> {
    let item = format_view_item_fragment(yaml)?;
    if let Some(views) = named_region(&base.spans.top_level, "views") {
        if empty_flow_collection(&views.region, "[]") {
            if root_uses_flow_mapping(&base.raw) {
                let section = format!("views:\n{item}");
                push_splice(splices, views.region.span, flow_mapping_entry(&section)?);
                return Ok(());
            }
            let child = item
                .trim_end_matches(['\n', '\r'])
                .strip_prefix("  ")
                .unwrap_or(item.trim_end_matches(['\n', '\r']));
            push_splice(
                splices,
                views.region.span,
                expand_empty_collection_region(&views.region, "views", child),
            );
            return Ok(());
        }
        if let Some((offset, has_trailing_comma, continuation_padding)) =
            flow_collection_append_point(&views.region, "views", FlowCollectionKind::Sequence)
        {
            let delimiter = if has_trailing_comma { "" } else { ", " };
            let padding = " ".repeat(continuation_padding);
            push_splice(
                splices,
                Span {
                    start: offset,
                    end: offset,
                },
                format!("{padding}{delimiter}{}", flow_view_item(yaml)?),
            );
            return Ok(());
        }
        let item = base
            .spans
            .views
            .last()
            .map(|view| reindent_view_item(&item, view.entry.text.as_str()))
            .unwrap_or(item);
        push_insertion_splice(splices, &base.raw, views.region.span.end, item);
        return Ok(());
    }

    let mut section = String::from("views:\n");
    section.push_str(&item);
    if push_root_flow_mapping_entry(base, &section, splices)? {
        return Ok(());
    }
    push_insertion_splice(splices, &base.raw, base.raw.len() as u32, section);
    Ok(())
}

fn push_remove_view_splice(
    base: &BaseFile,
    view: usize,
    splices: &mut Vec<Splice>,
) -> Result<(), SerializeError> {
    let entry = view_spans_for(base, view)?.entry.clone();
    if named_region(&base.spans.top_level, "views").is_some_and(|views| {
        flow_collection_close(&views.region, "views", FlowCollectionKind::Sequence).is_some()
    }) {
        let spans = base
            .spans
            .views
            .iter()
            .map(|view| view.entry.span)
            .collect::<Vec<_>>();
        return push_flow_item_removal(&base.raw, &spans, view, splices);
    }
    if base.spans.views.len() == 1 {
        let views =
            named_region(&base.spans.top_level, "views").ok_or_else(|| missing_span("views"))?;
        push_block_parent_collapse(splices, &base.raw, &views.region, "views", "[]", entry.span)?;
        return Ok(());
    }
    push_block_removal_splice(splices, &base.raw, entry.span);
    Ok(())
}

fn push_remove_view_key_splice(
    base: &BaseFile,
    view: usize,
    key: &str,
    splices: &mut Vec<Splice>,
) -> Result<(), SerializeError> {
    let view_spans = view_spans_for(base, view)?;
    let Some(region) = named_region(&view_spans.keys, key) else {
        return Ok(());
    };
    if let Some(root) = source_document(&base.raw)
        && let Some(views) = mapping_value(&root, "views")
        && let SourceNodeKind::Sequence { items, .. } = &views.kind
        && let Some(view_node) = items.get(view)
        && let SourceNodeKind::Mapping {
            flow: true,
            entries,
        } = &view_node.kind
    {
        let spans = entries
            .iter()
            .map(|entry| Span {
                start: entry.key.start as u32,
                end: entry.value.end as u32,
            })
            .collect::<Vec<_>>();
        let index = entries
            .iter()
            .position(
                |entry| matches!(&entry.key.kind, SourceNodeKind::Scalar { value } if value == key),
            )
            .ok_or_else(|| missing_span(format!("view {view} key {key:?}")))?;
        return push_flow_item_removal(&base.raw, &spans, index, splices);
    }
    push_block_removal_splice(splices, &base.raw, region.region.span);
    Ok(())
}

fn push_flow_item_removal(
    source: &str,
    item_spans: &[Span],
    item: usize,
    splices: &mut Vec<Splice>,
) -> Result<(), SerializeError> {
    let span = *item_spans
        .get(item)
        .ok_or_else(|| missing_span(format!("flow item {item}")))?;
    if item_spans.len() > 1 {
        let (between_start, between_end) = if let Some(next) = item_spans.get(item + 1) {
            (span.end as usize, next.start as usize)
        } else {
            (item_spans[item - 1].end as usize, span.start as usize)
        };
        let between = source
            .get(between_start..between_end)
            .ok_or_else(|| missing_span(format!("flow delimiter for item {item}")))?;
        let comma = between
            .find(',')
            .ok_or_else(|| SerializeError::InvalidEdit {
                message: format!("missing flow delimiter for item {item}"),
            })?
            + between_start;
        push_splice(
            splices,
            Span {
                start: comma as u32,
                end: comma as u32 + 1,
            },
            String::new(),
        );
    }
    push_splice(splices, span, String::new());
    Ok(())
}

fn push_view_key_insertion(
    base: &BaseFile,
    view: usize,
    view_spans: &ViewSpans,
    fragment: &str,
    splices: &mut Vec<Splice>,
) -> Result<(), SerializeError> {
    if let Some(root) = source_document(&base.raw)
        && let Some(views) = mapping_value(&root, "views")
        && let SourceNodeKind::Sequence { items, .. } = &views.kind
        && let Some(view_node) = items.get(view)
        && let SourceNodeKind::Mapping {
            flow: true,
            entries,
        } = &view_node.kind
    {
        let close = view_node
            .end
            .checked_sub(1)
            .ok_or_else(|| missing_span(format!("view {view} flow close")))?;
        push_splice(
            splices,
            Span {
                start: close as u32,
                end: close as u32,
            },
            format!(
                "{}{}",
                if entries.is_empty() { "" } else { ", " },
                flow_mapping_entry(fragment)?
            ),
        );
        return Ok(());
    }
    let (_, key_prefix) = region_prefixes(&view_spans.entry.text);
    push_insertion_splice(
        splices,
        &base.raw,
        view_spans.entry.span.end,
        format_yaml_fragment(fragment, &key_prefix, &key_prefix),
    );
    Ok(())
}

fn flow_mapping_entry(fragment: &str) -> Result<String, SerializeError> {
    let documents = yaml_rust2::YamlLoader::load_from_str(fragment).map_err(|error| {
        SerializeError::InvalidEdit {
            message: format!("view key YAML is invalid: {error}"),
        }
    })?;
    let Some(Yaml::Hash(mapping)) = documents.first() else {
        return Err(SerializeError::InvalidEdit {
            message: "view key YAML must be a mapping fragment".to_string(),
        });
    };
    if mapping.len() != 1 {
        return Err(SerializeError::InvalidEdit {
            message: "view key YAML must contain exactly one entry".to_string(),
        });
    }
    let (key, value) = mapping.iter().next().expect("single-entry mapping");
    Ok(format!(
        "{}: {}",
        render_flow_yaml(key)?,
        render_flow_yaml(value)?
    ))
}

fn ensure_view_key_is_editable(
    base: &BaseFile,
    view: usize,
    key: &str,
) -> Result<(), SerializeError> {
    let view_spans = view_spans_for(base, view)?;
    let Some(region) = named_region(&view_spans.keys, key) else {
        return Ok(());
    };
    let Some(view_def) = base.views.get(view) else {
        return Ok(());
    };
    if view_def
        .preserved
        .regions
        .iter()
        .any(|preserved| preserved.span == region.region.span)
    {
        return Err(SerializeError::WouldClobber {
            span: region.region.span,
            reason: format!("edit would rewrite preserved view key {key:?}"),
        });
    }
    Ok(())
}

fn ensure_set_view_key_is_closed(key: &str) -> Result<(), SerializeError> {
    if matches!(
        key,
        "type" | "name" | "limit" | "groupBy" | "order" | "source"
    ) {
        Ok(())
    } else {
        Err(SerializeError::InvalidEdit {
            message: format!(
                "SetViewKey only supports type/name/limit/groupBy/order; use the dedicated edit for {key:?}"
            ),
        })
    }
}

fn ensure_remove_view_key_is_closed(key: &str) -> Result<(), SerializeError> {
    if matches!(
        key,
        "filters" | "groupBy" | "limit" | "order" | "slate" | "source"
    ) {
        Ok(())
    } else {
        Err(SerializeError::InvalidEdit {
            message: format!("RemoveViewKey cannot remove required or unknown view key {key:?}"),
        })
    }
}

fn apply_splices(source: &str, mut splices: Vec<Splice>) -> Result<String, SerializeError> {
    splices.sort_by_key(|splice| (splice.span.start, splice.span.end, splice.order));

    let mut last_end = 0usize;
    for splice in &splices {
        let (start, end) = span_range(splice.span, source.len())?;
        if start < last_end {
            return Err(SerializeError::InvalidEdit {
                message: "edits target overlapping source spans".to_string(),
            });
        }
        last_end = end;
    }

    let mut out = source.to_string();
    for splice in splices.into_iter().rev() {
        let (start, end) = span_range(splice.span, source.len())?;
        out.replace_range(start..end, &splice.replacement);
    }
    Ok(out)
}

fn push_splice(splices: &mut Vec<Splice>, span: Span, replacement: String) {
    splices.push(Splice {
        span,
        replacement,
        order: splices.len(),
    });
}

fn push_block_removal_splice(splices: &mut Vec<Splice>, source: &str, mut span: Span) {
    span = block_removal_span_preserving_eof(source, span);
    push_splice(splices, span, String::new());
}

fn push_block_parent_collapse(
    splices: &mut Vec<Splice>,
    source: &str,
    parent: &PreservedRegion,
    fallback_key: &str,
    empty_value: &str,
    child: Span,
) -> Result<(), SerializeError> {
    let newline_offset = parent
        .text
        .find('\n')
        .ok_or_else(|| missing_span(format!("{fallback_key} parent line")))?;
    let line_end = if newline_offset > 0 && parent.text.as_bytes()[newline_offset - 1] == b'\r' {
        newline_offset - 1
    } else {
        newline_offset
    };
    let line = &parent.text[..line_end];
    let trimmed = line.trim_start();
    let indent = &line[..line.len() - trimmed.len()];
    let (authored_key, comment) = key_colon_index(trimmed).map_or_else(
        || (fallback_key, ""),
        |colon| {
            let (_, comment) = split_inline_comment(&trimmed[colon + 1..]);
            (&trimmed[..colon], comment)
        },
    );
    let line_span = Span {
        start: parent.span.start,
        end: parent
            .span
            .start
            .checked_add((newline_offset + 1) as u32)
            .ok_or_else(|| missing_span(format!("{fallback_key} parent line")))?,
    };
    if child.start < line_span.end {
        return Err(missing_span(format!(
            "{fallback_key} child after parent line"
        )));
    }
    let child_is_adjacent = child.start == line_span.end;
    let omit_newline =
        child_is_adjacent && child.end as usize == source.len() && !source.ends_with('\n');
    let line_ending = if omit_newline {
        ""
    } else if parent.text[..=newline_offset].ends_with("\r\n") {
        "\r\n"
    } else {
        "\n"
    };
    push_splice(
        splices,
        line_span,
        format!("{indent}{authored_key}: {empty_value}{comment}{line_ending}"),
    );
    if child_is_adjacent {
        push_splice(splices, child, String::new());
    } else {
        push_block_removal_splice(splices, source, child);
    }
    Ok(())
}

fn block_removal_span_preserving_eof(source: &str, mut span: Span) -> Span {
    if span.end as usize == source.len() && !source.ends_with('\n') {
        let start = span.start as usize;
        span.start = if source[..start].ends_with("\r\n") {
            span.start.saturating_sub(2)
        } else if source[..start].ends_with('\n') {
            span.start.saturating_sub(1)
        } else {
            span.start
        };
    }
    span
}

fn push_insertion_splice(splices: &mut Vec<Splice>, source: &str, offset: u32, text: String) {
    let offset_usize = offset as usize;
    let newline = if source.contains("\r\n") {
        "\r\n"
    } else {
        "\n"
    };
    let mut text = text.replace("\r\n", "\n");
    if newline == "\r\n" {
        text = text.replace('\n', "\r\n");
    }
    if offset_usize == source.len() && !source.ends_with('\n') {
        text.truncate(text.trim_end_matches(['\r', '\n']).len());
    }
    let mut replacement = String::new();
    if offset_usize > 0 && !source[..offset_usize].ends_with('\n') {
        replacement.push_str(newline);
    }
    replacement.push_str(&text);
    push_splice(
        splices,
        Span {
            start: offset,
            end: offset,
        },
        replacement,
    );
}

fn span_range(span: Span, source_len: usize) -> Result<(usize, usize), SerializeError> {
    let start = span.start as usize;
    let end = span.end as usize;
    if start > end || end > source_len {
        return Err(SerializeError::MissingSpan {
            target: format!("{}..{}", span.start, span.end),
        });
    }
    Ok((start, end))
}

fn view_spans_for(base: &BaseFile, view: usize) -> Result<&ViewSpans, SerializeError> {
    base.spans
        .views
        .get(view)
        .ok_or_else(|| missing_span(format!("view {view}")))
}

fn named_region<'a>(regions: &'a [NamedRegion], name: &str) -> Option<&'a NamedRegion> {
    regions.iter().find(|region| region.name == name)
}

fn missing_span(target: impl Into<String>) -> SerializeError {
    SerializeError::MissingSpan {
        target: target.into(),
    }
}

fn key_value_fragment(key: &str, value: &str) -> String {
    if fragment_starts_with_key(value, key) {
        value.to_string()
    } else {
        format!("{}: {}", yaml_key(key), value.trim_end())
    }
}

fn editable_view_scalar(key: &str, value: &str) -> Option<String> {
    if !matches!(key, "type" | "name" | "source")
        || value.contains(['\n', '\r'])
        || fragment_starts_with_key(value, key)
    {
        return None;
    }
    let trimmed = value.trim();
    let parsed = yaml_rust2::YamlLoader::load_from_str(trimmed)
        .ok()
        .and_then(|documents| documents.into_iter().next());
    Some(match parsed {
        Some(Yaml::String(value)) => value,
        _ => trimmed.to_string(),
    })
}

fn push_scalar_replacement(
    splices: &mut Vec<Splice>,
    region: &PreservedRegion,
    value: &str,
) -> bool {
    let Some((span, existing)) = scalar_tail(region) else {
        return false;
    };
    push_splice(splices, span, replacement_scalar(existing, value));
    true
}

fn scalar_tail(region: &PreservedRegion) -> Option<(Span, &str)> {
    let line_end = region.text.find(['\r', '\n']).unwrap_or(region.text.len());
    let line = &region.text[..line_end];
    let candidate = line
        .trim_start()
        .strip_prefix("- ")
        .unwrap_or(line.trim_start());
    let candidate_offset = line.len() - candidate.len();
    let colon = key_colon_index(candidate)?;
    let mut value_start = candidate_offset + colon + 1;
    while line
        .as_bytes()
        .get(value_start)
        .is_some_and(u8::is_ascii_whitespace)
    {
        value_start += 1;
    }
    if value_start >= line_end || matches!(line.as_bytes()[value_start], b'|' | b'>') {
        return None;
    }
    let value_end = match line.as_bytes()[value_start] {
        b'\'' => quoted_scalar_end(region.text.as_bytes(), value_start, b'\'', true),
        b'"' => quoted_scalar_end(region.text.as_bytes(), value_start, b'"', false),
        _ => plain_scalar_end(&region.text, value_start),
    };
    if value_end <= value_start || value_end > region.text.len() {
        return None;
    }
    Some((
        Span {
            start: region.span.start + value_start as u32,
            end: region.span.start + value_end as u32,
        },
        &region.text[value_start..value_end],
    ))
}

fn plain_scalar_end(source: &str, start: usize) -> usize {
    let mut cursor = start;
    let mut end = start;
    while cursor < source.len() {
        let newline = source[cursor..]
            .find('\n')
            .map_or(source.len(), |offset| cursor + offset);
        let line_end = if newline > cursor && source.as_bytes()[newline - 1] == b'\r' {
            newline - 1
        } else {
            newline
        };
        let line = &source[cursor..line_end];
        if line.trim_start().starts_with('#') {
            break;
        }
        let (body, comment) = split_inline_comment(line);
        let body_end = cursor + body.trim_end_matches(char::is_whitespace).len();
        if body_end > cursor {
            end = body_end;
        }
        if !comment.is_empty() || newline == source.len() {
            break;
        }
        cursor = newline + 1;
    }
    end
}

fn replacement_scalar(existing: &str, value: &str) -> String {
    let (body, comment) = split_inline_comment(existing);
    let rendered = match body.trim().chars().next() {
        Some('\'') => quote_single_yaml(value),
        Some('"') => quote_double_yaml(value),
        _ => yaml_scalar(value),
    };
    format!("{rendered}{comment}")
}

fn split_inline_comment(value: &str) -> (&str, &str) {
    let mut quote = None;
    let mut escaped = false;
    for (index, ch) in value.char_indices() {
        match quote {
            Some('"') if escaped => escaped = false,
            Some('"') if ch == '\\' => escaped = true,
            Some(active) if ch == active => quote = None,
            Some(_) => {}
            None if matches!(ch, '\'' | '"') => quote = Some(ch),
            None if ch == '#' && (index == 0 || value[..index].ends_with(char::is_whitespace)) => {
                let comment_start = value[..index].trim_end_matches(char::is_whitespace).len();
                return (&value[..comment_start], &value[comment_start..]);
            }
            None => {}
        }
    }
    (value, "")
}

fn quote_single_yaml(value: &str) -> String {
    format!("'{}'", value.replace('\'', "''"))
}

fn quote_double_yaml(value: &str) -> String {
    quote_yaml_string(value)
}

fn yaml_scalar(value: &str) -> String {
    let plain_is_string = yaml_rust2::YamlLoader::load_from_str(value)
        .ok()
        .and_then(|documents| documents.into_iter().next())
        .is_some_and(|yaml| matches!(yaml, Yaml::String(ref parsed) if parsed == value));
    let unsafe_plain = value.is_empty()
        || value.trim() != value
        || value.contains(['\n', '\r'])
        || value.contains(": ")
        || value.contains(" #")
        || value
            .chars()
            .next()
            .is_some_and(|ch| "-?:,[]{}#&*!|>'\"%@`".contains(ch));
    if plain_is_string && !unsafe_plain {
        value.to_string()
    } else {
        quote_double_yaml(value)
    }
}

fn empty_flow_collection(region: &PreservedRegion, token: &str) -> bool {
    let line = region.text.lines().next().unwrap_or_default().trim_start();
    let Some(colon) = key_colon_index(line) else {
        return false;
    };
    let (body, _) = split_inline_comment(&line[colon + 1..]);
    body.trim() == token
}

#[derive(Clone, Copy)]
enum FlowCollectionKind {
    Mapping,
    Sequence,
}

fn flow_collection_close(
    region: &PreservedRegion,
    key: &str,
    expected: FlowCollectionKind,
) -> Option<u32> {
    let root = source_document(&region.text)?;
    let value = mapping_value(&root, key)?;
    let closing = match (&value.kind, expected) {
        (SourceNodeKind::Mapping { flow: true, .. }, FlowCollectionKind::Mapping) => b'}',
        (SourceNodeKind::Sequence { flow: true, .. }, FlowCollectionKind::Sequence) => b']',
        _ => return None,
    };
    let close = value.end.checked_sub(1)?;
    if region.text.as_bytes().get(close) != Some(&closing) {
        return None;
    }
    region.span.start.checked_add(u32::try_from(close).ok()?)
}

fn flow_collection_append_point(
    region: &PreservedRegion,
    key: &str,
    expected: FlowCollectionKind,
) -> Option<(u32, bool, usize)> {
    let root = source_document(&region.text)?;
    let value = mapping_value(&root, key)?;
    let (closing, last_end) = match (&value.kind, expected) {
        (
            SourceNodeKind::Mapping {
                flow: true,
                entries,
            },
            FlowCollectionKind::Mapping,
        ) => (b'}', entries.last().map(|entry| entry.value.end)),
        (SourceNodeKind::Sequence { flow: true, items }, FlowCollectionKind::Sequence) => {
            (b']', items.last().map(|item| item.end))
        }
        _ => return None,
    };
    let close = value.end.checked_sub(1)?;
    if region.text.as_bytes().get(close) != Some(&closing) {
        return None;
    }
    let offset = region.span.start.checked_add(u32::try_from(close).ok()?)?;
    let has_trailing_comma =
        last_end.is_some_and(|last_end| flow_suffix_has_comma(&region.text, last_end, close));
    let continuation_padding = flow_continuation_padding(&region.text, value.start, close);
    Some((offset, has_trailing_comma, continuation_padding))
}

fn flow_continuation_padding(source: &str, value_start: usize, close: usize) -> usize {
    let value_line_start = line_start(source, value_start);
    let close_line_start = line_start(source, close);
    if value_line_start == close_line_start {
        return 0;
    }
    let key_indent = source[value_line_start..value_start]
        .bytes()
        .take_while(u8::is_ascii_whitespace)
        .count();
    let close_prefix = &source[close_line_start..close];
    if !close_prefix.bytes().all(|byte| byte.is_ascii_whitespace()) {
        return 0;
    }
    (key_indent + 2).saturating_sub(close_prefix.len())
}

fn flow_view_item(source: &str) -> Result<String, SerializeError> {
    let documents = yaml_rust2::YamlLoader::load_from_str(source).map_err(|error| {
        SerializeError::InvalidEdit {
            message: format!("view YAML is invalid: {error}"),
        }
    })?;
    if documents.len() != 1 {
        return Err(SerializeError::InvalidEdit {
            message: "view YAML must contain exactly one document".to_string(),
        });
    }
    let document = documents
        .first()
        .ok_or_else(|| SerializeError::InvalidEdit {
            message: "view YAML cannot be empty".to_string(),
        })?;
    let view = match document {
        Yaml::Array(items) if items.len() == 1 => &items[0],
        other => other,
    };
    if !matches!(view, Yaml::Hash(_)) {
        return Err(SerializeError::InvalidEdit {
            message: "view YAML must be a mapping".to_string(),
        });
    }
    render_flow_yaml(view)
}

fn render_flow_yaml(value: &Yaml) -> Result<String, SerializeError> {
    match value {
        Yaml::String(value) => Ok(quote_yaml_string(value)),
        Yaml::Integer(value) => Ok(value.to_string()),
        Yaml::Real(value) => Ok(value.clone()),
        Yaml::Boolean(value) => Ok(value.to_string()),
        Yaml::Array(items) => {
            let rendered = items
                .iter()
                .map(render_flow_yaml)
                .collect::<Result<Vec<_>, _>>()?;
            Ok(format!("[{}]", rendered.join(", ")))
        }
        Yaml::Hash(entries) => {
            let mut rendered = Vec::with_capacity(entries.len());
            for (key, value) in entries {
                rendered.push(format!(
                    "{}: {}",
                    render_flow_yaml(key)?,
                    render_flow_yaml(value)?
                ));
            }
            Ok(format!("{{{}}}", rendered.join(", ")))
        }
        Yaml::Null => Ok("null".to_string()),
        Yaml::Alias(_) | Yaml::BadValue => Err(SerializeError::InvalidEdit {
            message: "view YAML contains a value that cannot be emitted safely in flow style"
                .to_string(),
        }),
    }
}

fn expand_empty_collection_region(
    region: &PreservedRegion,
    fallback_key: &str,
    child: &str,
) -> String {
    let first_line = region.text.lines().next().unwrap_or_default();
    let trimmed = first_line.trim_start();
    let indent_len = first_line.len() - trimmed.len();
    let (key, comment) = key_colon_index(trimmed).map_or_else(
        || (fallback_key, ""),
        |colon| {
            let (_, comment) = split_inline_comment(&trimmed[colon + 1..]);
            (&trimmed[..colon], comment)
        },
    );
    let indent = &first_line[..indent_len];
    let child = child
        .lines()
        .enumerate()
        .map(|(index, line)| {
            if index == 0 {
                line.to_string()
            } else {
                format!("{indent}{line}")
            }
        })
        .collect::<Vec<_>>()
        .join("\n");
    let mut expanded = expand_empty_collection(indent, key, &child);
    if !comment.is_empty()
        && let Some(newline) = expanded.find('\n')
    {
        expanded.insert_str(newline, comment);
    }
    if region.text.contains("\r\n") {
        expanded = expanded.replace('\n', "\r\n");
    }
    if region.text.ends_with('\n') {
        expanded.push_str(if region.text.ends_with("\r\n") {
            "\r\n"
        } else {
            "\n"
        });
    }
    expanded
}

fn expand_empty_collection(key_indent: &str, key: &str, child: &str) -> String {
    format!("{key_indent}{key}:\n{key_indent}  {child}")
}

fn fragment_starts_with_key(fragment: &str, key: &str) -> bool {
    fragment
        .lines()
        .find(|line| !line.trim().is_empty())
        .and_then(key_on_line)
        .is_some_and(|(candidate, _)| candidate == key)
}

fn format_fragment_for_region(region: &PreservedRegion, fragment: &str) -> String {
    let (first_prefix, continuation_prefix) = region_prefixes(&region.text);
    let mut formatted = format_yaml_fragment(fragment, &first_prefix, &continuation_prefix);
    if region.text.contains("\r\n") {
        formatted = formatted.replace('\n', "\r\n");
    }
    if !region.text.ends_with('\n') {
        formatted.truncate(formatted.trim_end_matches(['\r', '\n']).len());
    }
    formatted
}

fn format_yaml_fragment(fragment: &str, first_prefix: &str, continuation_prefix: &str) -> String {
    let fragment = fragment.trim_end_matches(['\n', '\r']);
    if fragment.is_empty() {
        return String::new();
    }

    let mut out = String::new();
    for (idx, line) in fragment.lines().enumerate() {
        if idx == 0 {
            out.push_str(first_prefix);
        } else if !line.is_empty() {
            out.push_str(continuation_prefix);
        }
        out.push_str(line);
        out.push('\n');
    }
    out
}

fn region_prefixes(region_text: &str) -> (String, String) {
    let first = region_text.lines().next().unwrap_or_default();
    let trimmed = first.trim_start();
    let leading = first.len() - trimmed.len();
    if trimmed.starts_with("- ") {
        (
            format!("{}- ", " ".repeat(leading)),
            " ".repeat(leading + 2),
        )
    } else {
        let prefix = " ".repeat(leading);
        (prefix.clone(), prefix)
    }
}

fn format_view_item_fragment(yaml: &str) -> Result<String, SerializeError> {
    let yaml = yaml.trim_end_matches(['\n', '\r']);
    if yaml.trim().is_empty() {
        return Err(SerializeError::InvalidEdit {
            message: "view YAML cannot be empty".to_string(),
        });
    }
    let Some((first, rest)) = yaml.split_once('\n') else {
        let first = yaml.trim_start();
        if first.starts_with("- ") {
            return Ok(format!("  {first}\n"));
        }
        return Ok(format!("  - {first}\n"));
    };

    let mut out = String::new();
    let first = first.trim_start();
    if first.starts_with("- ") {
        out.push_str("  ");
        out.push_str(first);
        out.push('\n');
        for line in rest.lines() {
            out.push_str("  ");
            out.push_str(line);
            out.push('\n');
        }
    } else {
        out.push_str("  - ");
        out.push_str(first);
        out.push('\n');
        for line in rest.lines() {
            if line.is_empty() {
                out.push('\n');
            } else {
                out.push_str("    ");
                out.push_str(line);
                out.push('\n');
            }
        }
    }
    Ok(out)
}

fn reindent_view_item(item: &str, authored_view: &str) -> String {
    let authored_indent = authored_view
        .lines()
        .next()
        .map(|line| line.len() - line.trim_start().len())
        .unwrap_or(2);
    if authored_indent == 2 {
        return item.to_string();
    }
    let prefix = " ".repeat(authored_indent);
    item.lines()
        .map(|line| format!("{prefix}{}\n", line.strip_prefix("  ").unwrap_or(line)))
        .collect()
}

fn quote_yaml_string(value: &str) -> String {
    let mut out = String::from("\"");
    for ch in value.chars() {
        match ch {
            '\\' => out.push_str("\\\\"),
            '"' => out.push_str("\\\""),
            '\n' => out.push_str("\\n"),
            '\r' => out.push_str("\\r"),
            '\t' => out.push_str("\\t"),
            other => out.push(other),
        }
    }
    out.push('"');
    out
}

fn yaml_key(key: &str) -> String {
    if !key.is_empty()
        && key
            .chars()
            .all(|ch| ch.is_ascii_alphanumeric() || matches!(ch, '_' | '-' | '.'))
    {
        key.to_string()
    } else {
        quote_yaml_string(key)
    }
}

fn parse_expr_map(
    value: &Yaml,
    entry_regions: &HashMap<String, PreservedRegion>,
    warning_kind: BaseWarningKind,
    label: &str,
    out: &mut Vec<(String, Expr)>,
    sources: &mut HashMap<String, String>,
    warnings: &mut Vec<BaseWarning>,
) {
    let Yaml::Hash(map) = value else {
        warnings.push(BaseWarning {
            kind: warning_kind,
            message: format!("{label}s must be a YAML mapping"),
            span: None,
        });
        return;
    };

    for (key_yaml, value) in map {
        let name = yaml_key_to_string(key_yaml);
        let Some(source) = yaml_string(value) else {
            warnings.push(BaseWarning {
                kind: warning_kind,
                message: format!("{label} {name:?} must be a string expression"),
                span: None,
            });
            let raw = entry_regions
                .get(&name)
                .map(|region| region.text.clone())
                .unwrap_or_else(|| yaml_value_to_string(value));
            out.push((
                name.clone(),
                unsupported_expr(&raw, format!("{label} expression must be a YAML string")),
            ));
            sources.insert(name, raw);
            continue;
        };
        let expr = parse_expression_string(source, warning_kind, warnings);
        sources.insert(name.clone(), source.to_string());
        out.push((name, expr));
    }
}

fn parse_properties(
    value: &Yaml,
    property_regions: &HashMap<String, PreservedRegion>,
    source: &str,
    warnings: &mut Vec<BaseWarning>,
) -> Vec<(String, PropertyConfig)> {
    let Yaml::Hash(map) = value else {
        warnings.push(BaseWarning {
            kind: BaseWarningKind::InvalidProperty,
            message: "properties must be a YAML mapping".to_string(),
            span: None,
        });
        return Vec::new();
    };

    map.iter()
        .map(|(key_yaml, value)| {
            let id = yaml_key_to_string(key_yaml);
            let mut config = PropertyConfig {
                display_name: None,
                preserved: PreservedYaml::default(),
            };
            match value {
                Yaml::Hash(config_map) => {
                    let sub_regions = property_regions
                        .get(&id)
                        .map(|region| regions_in_span(source, region.span, 4))
                        .unwrap_or_default();
                    for (sub_key_yaml, sub_value) in config_map {
                        let sub_key = yaml_key_to_string(sub_key_yaml);
                        if sub_key == "displayName" {
                            config.display_name = yaml_string(sub_value).map(str::to_string);
                        } else if let Some(region) = sub_regions.get(&sub_key) {
                            config.preserved.regions.push(region.clone());
                        }
                    }
                }
                _ => warnings.push(BaseWarning {
                    kind: BaseWarningKind::InvalidProperty,
                    message: format!("property {id:?} config must be a mapping"),
                    span: None,
                }),
            }
            (id, config)
        })
        .collect()
}

fn parse_views(
    value: &Yaml,
    view_spans: &[ViewSpans],
    source: &str,
    custom_summaries: &[(String, Expr)],
    warnings: &mut Vec<BaseWarning>,
) -> Vec<ViewDef> {
    let Yaml::Array(items) = value else {
        warnings.push(BaseWarning {
            kind: BaseWarningKind::InvalidView,
            message: "views must be a YAML list".to_string(),
            span: None,
        });
        return Vec::new();
    };

    let custom_summary_names: HashSet<&str> = custom_summaries
        .iter()
        .map(|(name, _)| name.as_str())
        .collect();
    let mut seen_names = HashSet::new();
    let mut views = Vec::new();

    for (idx, item) in items.iter().enumerate() {
        let mut view = ViewDef {
            view_type: ViewType::Table,
            name: format!("View {}", idx + 1),
            limit: None,
            filters: None,
            group_by: None,
            order: Vec::new(),
            summaries: Vec::new(),
            source: RowSource::Files,
            slate_state: None,
            preserved: PreservedYaml::default(),
        };
        let Yaml::Hash(map) = item else {
            warnings.push(BaseWarning {
                kind: BaseWarningKind::InvalidView,
                message: format!("view {} must be a mapping", idx + 1),
                span: None,
            });
            views.push(view);
            continue;
        };

        let mut has_name = false;
        let mut has_type = false;
        let item_region = view_spans.get(idx).map(|spans| spans.entry.clone());
        let item_key_regions = view_spans
            .get(idx)
            .map(|spans| named_region_map(&spans.keys))
            .unwrap_or_default();

        for (key_yaml, value) in map {
            let key = yaml_key_to_string(key_yaml);
            match key.as_str() {
                "type" => {
                    has_type = true;
                    view.view_type = parse_view_type(yaml_string(value).unwrap_or_default());
                }
                "name" => {
                    if let Some(name) = yaml_string(value) {
                        view.name = name.to_string();
                        has_name = true;
                    }
                }
                "limit" => {
                    view.limit = parse_limit(value, warnings);
                }
                "filters" => {
                    let filter_regions = item_key_regions
                        .get("filters")
                        .map(|region| filter_node_regions_in_span(source, region.span))
                        .unwrap_or_default();
                    let mut region_cursor = 0usize;
                    view.filters = Some(parse_filter_node(
                        value,
                        &filter_regions,
                        &mut region_cursor,
                        warnings,
                    ));
                }
                "groupBy" => {
                    view.group_by = parse_group_by(value, warnings);
                }
                "order" => {
                    view.order = parse_string_list(value, warnings, "order");
                }
                "summaries" => {
                    view.summaries = parse_view_summaries(value, &custom_summary_names, warnings);
                }
                "source" => {
                    view.source = parse_row_source(value, warnings);
                }
                "slate" => {
                    view.slate_state = Some(yaml_to_json(value));
                }
                _ => {
                    view.preserved.regions.push(
                        item_key_regions
                            .get(&key)
                            .cloned()
                            .or_else(|| item_region.clone())
                            .unwrap_or_else(|| preserved_region(source, 0, source.len())),
                    );
                }
            }
        }

        if !has_type {
            warnings.push(BaseWarning {
                kind: BaseWarningKind::MissingViewType,
                message: format!("view {} missing type; defaulting to table", idx + 1),
                span: None,
            });
        }
        if !has_name {
            warnings.push(BaseWarning {
                kind: BaseWarningKind::MissingViewName,
                message: format!("view {} missing name; synthesized {:?}", idx + 1, view.name),
                span: None,
            });
        }
        if !seen_names.insert(view.name.clone()) {
            warnings.push(BaseWarning {
                kind: BaseWarningKind::DuplicateViewName,
                message: format!("duplicate view name {:?}", view.name),
                span: None,
            });
        }
        views.push(view);
    }

    views
}

fn parse_filter_node(
    value: &Yaml,
    raw_regions: &[PreservedRegion],
    raw_cursor: &mut usize,
    warnings: &mut Vec<BaseWarning>,
) -> FilterNode {
    let raw_region = raw_regions
        .get(*raw_cursor)
        .map(|region| region.text.as_str());
    *raw_cursor += 1;

    if let Some(source) = yaml_string(value) {
        return FilterNode::Stmt(parse_expression_string(
            source,
            BaseWarningKind::InvalidExpression,
            warnings,
        ));
    }

    let Yaml::Hash(map) = value else {
        warnings.push(BaseWarning {
            kind: BaseWarningKind::InvalidFilter,
            message: format!(
                "filter node must be a string or mapping, got {}",
                yaml_type_name(value)
            ),
            span: None,
        });
        let raw = raw_region
            .map(str::to_string)
            .unwrap_or_else(|| yaml_value_to_string(value));
        return FilterNode::Stmt(unsupported_expr(&raw, "invalid filter node"));
    };

    let combinators: Vec<(&str, &Yaml)> = map
        .iter()
        .filter_map(|(key, value)| match yaml_key_to_string(key).as_str() {
            "and" => Some(("and", value)),
            "or" => Some(("or", value)),
            "not" => Some(("not", value)),
            _ => None,
        })
        .collect();
    if combinators.len() != 1 || map.len() != 1 {
        warnings.push(BaseWarning {
            kind: BaseWarningKind::InvalidFilter,
            message: "filter mapping must contain exactly one of and/or/not".to_string(),
            span: None,
        });
        let raw = raw_region
            .map(str::to_string)
            .unwrap_or_else(|| yaml_value_to_string(value));
        return FilterNode::Stmt(unsupported_expr(&raw, "invalid filter combinator"));
    }

    let (kind, list_value) = combinators[0];
    let Yaml::Array(items) = list_value else {
        warnings.push(BaseWarning {
            kind: BaseWarningKind::InvalidFilter,
            message: format!("{kind} filter must contain a list"),
            span: None,
        });
        let raw = raw_region
            .map(str::to_string)
            .unwrap_or_else(|| yaml_value_to_string(value));
        return FilterNode::Stmt(unsupported_expr(&raw, "invalid filter combinator value"));
    };
    let parsed = items
        .iter()
        .map(|item| parse_filter_node(item, raw_regions, raw_cursor, warnings))
        .collect();
    match kind {
        "and" => FilterNode::And(parsed),
        "or" => FilterNode::Or(parsed),
        "not" => FilterNode::Not(parsed),
        _ => unreachable!("filtered above"),
    }
}

fn parse_expression_string(
    source: &str,
    warning_kind: BaseWarningKind,
    warnings: &mut Vec<BaseWarning>,
) -> Expr {
    parse_expr(source).unwrap_or_else(|err| {
        warnings.push(BaseWarning {
            kind: warning_kind,
            message: err.to_string(),
            span: Some(err.span),
        });
        unsupported_expr(source, err.message)
    })
}

fn parse_view_type(value: &str) -> ViewType {
    match value {
        "table" => ViewType::Table,
        "list" => ViewType::List,
        "cards" => ViewType::Cards,
        "map" => ViewType::Map,
        other => ViewType::Other(other.to_string()),
    }
}

fn parse_limit(value: &Yaml, warnings: &mut Vec<BaseWarning>) -> Option<u64> {
    match value {
        Yaml::Integer(i) if *i >= 0 => Some(*i as u64),
        Yaml::String(s) => match s.parse::<u64>() {
            Ok(limit) => Some(limit),
            Err(_) => {
                warnings.push(BaseWarning {
                    kind: BaseWarningKind::InvalidLimit,
                    message: format!("invalid view limit {s:?}"),
                    span: None,
                });
                None
            }
        },
        _ => {
            warnings.push(BaseWarning {
                kind: BaseWarningKind::InvalidLimit,
                message: "view limit must be a non-negative integer".to_string(),
                span: None,
            });
            None
        }
    }
}

fn parse_group_by(value: &Yaml, warnings: &mut Vec<BaseWarning>) -> Option<GroupBy> {
    let Yaml::Hash(map) = value else {
        warnings.push(BaseWarning {
            kind: BaseWarningKind::InvalidGroupBy,
            message: "groupBy must be a mapping".to_string(),
            span: None,
        });
        return None;
    };
    let mut property = None;
    let mut ascending = true;
    for (key_yaml, value) in map {
        match yaml_key_to_string(key_yaml).as_str() {
            "property" => {
                let Some(source) = yaml_string(value) else {
                    warnings.push(BaseWarning {
                        kind: BaseWarningKind::InvalidGroupBy,
                        message: "groupBy.property must be a string".to_string(),
                        span: None,
                    });
                    continue;
                };
                property = parse_property_ref(source, warnings);
            }
            "direction" => {
                if let Some(direction) = yaml_string(value) {
                    if direction.eq_ignore_ascii_case("ASC") {
                        ascending = true;
                    } else if direction.eq_ignore_ascii_case("DESC") {
                        ascending = false;
                    } else {
                        warnings.push(BaseWarning {
                            kind: BaseWarningKind::InvalidGroupBy,
                            message: format!(
                                "groupBy.direction must be ASC or DESC, got {direction:?}; defaulting to ASC"
                            ),
                            span: None,
                        });
                        ascending = true;
                    }
                } else {
                    warnings.push(BaseWarning {
                        kind: BaseWarningKind::InvalidGroupBy,
                        message: "groupBy.direction must be a string; defaulting to ASC"
                            .to_string(),
                        span: None,
                    });
                    ascending = true;
                }
            }
            _ => {}
        }
    }
    property.map(|property| GroupBy {
        property,
        ascending,
    })
}

fn parse_property_ref(source: &str, warnings: &mut Vec<BaseWarning>) -> Option<PropertyRef> {
    let expr = parse_expression_string(source, BaseWarningKind::InvalidGroupBy, warnings);
    match expr.kind {
        ExprKind::Prop(prop) => Some(prop),
        ExprKind::Unsupported { reason, .. } => {
            warnings.push(BaseWarning {
                kind: BaseWarningKind::InvalidGroupBy,
                message: reason,
                span: Some(expr.span),
            });
            None
        }
        _ => {
            warnings.push(BaseWarning {
                kind: BaseWarningKind::InvalidGroupBy,
                message: format!("groupBy.property must be a property reference, got {source:?}"),
                span: Some(expr.span),
            });
            None
        }
    }
}

fn parse_string_list(value: &Yaml, warnings: &mut Vec<BaseWarning>, label: &str) -> Vec<String> {
    let Yaml::Array(items) = value else {
        warnings.push(BaseWarning {
            kind: BaseWarningKind::InvalidView,
            message: format!("{label} must be a YAML list"),
            span: None,
        });
        return Vec::new();
    };
    items
        .iter()
        .filter_map(|item| match yaml_string(item) {
            Some(value) => Some(value.to_string()),
            None => {
                warnings.push(BaseWarning {
                    kind: BaseWarningKind::InvalidView,
                    message: format!("{label} entries must be strings"),
                    span: None,
                });
                None
            }
        })
        .collect()
}

fn parse_view_summaries(
    value: &Yaml,
    custom_summary_names: &HashSet<&str>,
    warnings: &mut Vec<BaseWarning>,
) -> Vec<(String, SummaryRef)> {
    let Yaml::Hash(map) = value else {
        warnings.push(BaseWarning {
            kind: BaseWarningKind::InvalidView,
            message: "view summaries must be a mapping".to_string(),
            span: None,
        });
        return Vec::new();
    };
    map.iter()
        .filter_map(|(key_yaml, value)| {
            let property = yaml_key_to_string(key_yaml);
            let Some(name) = yaml_string(value) else {
                warnings.push(BaseWarning {
                    kind: BaseWarningKind::InvalidView,
                    message: format!("summary assignment for {property:?} must be a string"),
                    span: None,
                });
                return None;
            };
            let summary = if is_builtin_summary(name) || !custom_summary_names.contains(name) {
                SummaryRef::Builtin(name.to_string())
            } else {
                SummaryRef::Custom(name.to_string())
            };
            Some((property, summary))
        })
        .collect()
}

fn parse_row_source(value: &Yaml, warnings: &mut Vec<BaseWarning>) -> RowSource {
    match yaml_string(value) {
        Some("files") => RowSource::Files,
        Some("tasks") => RowSource::Tasks,
        Some(other) => {
            warnings.push(BaseWarning {
                kind: BaseWarningKind::InvalidViewSource,
                message: format!("unknown view source {other:?}; defaulting to files"),
                span: None,
            });
            RowSource::Files
        }
        None => {
            warnings.push(BaseWarning {
                kind: BaseWarningKind::InvalidViewSource,
                message: "view source must be a string; defaulting to files".to_string(),
                span: None,
            });
            RowSource::Files
        }
    }
}

fn mark_circular_formulas(
    formulas: &mut [(String, Expr)],
    formula_sources: &HashMap<String, String>,
    warnings: &mut Vec<BaseWarning>,
) {
    let graph: HashMap<String, BTreeSet<String>> = formulas
        .iter()
        .map(|(name, expr)| {
            let mut refs = BTreeSet::new();
            collect_formula_refs(expr, &mut refs);
            (name.clone(), refs)
        })
        .collect();

    let names: Vec<String> = formulas.iter().map(|(name, _)| name.clone()).collect();
    let circular: BTreeSet<String> = names
        .iter()
        .filter(|name| reaches(name, name, &graph, &mut HashSet::new()))
        .cloned()
        .collect();

    if circular.is_empty() {
        return;
    }
    for (name, expr) in formulas.iter_mut() {
        if circular.contains(name) {
            let raw = formula_sources
                .get(name)
                .cloned()
                .unwrap_or_else(|| name.clone());
            *expr = unsupported_expr(&raw, "circular reference");
            warnings.push(BaseWarning {
                kind: BaseWarningKind::CircularFormula,
                message: format!("formula {name:?} participates in a circular reference"),
                span: Some(expr.span),
            });
        }
    }
}

fn reaches(
    current: &str,
    target: &str,
    graph: &HashMap<String, BTreeSet<String>>,
    seen: &mut HashSet<String>,
) -> bool {
    let Some(next) = graph.get(current) else {
        return false;
    };
    for candidate in next {
        if candidate == target {
            return true;
        }
        if seen.insert(candidate.clone()) && reaches(candidate, target, graph, seen) {
            return true;
        }
    }
    false
}

fn collect_formula_refs(expr: &Expr, refs: &mut BTreeSet<String>) {
    match &expr.kind {
        ExprKind::Prop(PropertyRef::Formula(name)) => {
            refs.insert(name.clone());
        }
        ExprKind::Index { base, index } => {
            collect_formula_refs(base, refs);
            collect_formula_refs(index, refs);
        }
        ExprKind::Field { base, .. } => collect_formula_refs(base, refs),
        ExprKind::Unary { rhs, .. } => collect_formula_refs(rhs, refs),
        ExprKind::Binary { lhs, rhs, .. } => {
            collect_formula_refs(lhs, refs);
            collect_formula_refs(rhs, refs);
        }
        ExprKind::Call { callee, args } => {
            if let Callee::Method { receiver, .. } = callee {
                collect_formula_refs(receiver, refs);
            }
            for arg in args {
                collect_formula_refs(arg, refs);
            }
        }
        ExprKind::ListExpr {
            base, body, init, ..
        } => {
            collect_formula_refs(base, refs);
            collect_formula_refs(body, refs);
            if let Some(init) = init {
                collect_formula_refs(init, refs);
            }
        }
        ExprKind::Lit(Lit::List(items)) => {
            for item in items {
                collect_formula_refs(item, refs);
            }
        }
        ExprKind::Lit(Lit::Object(items)) => {
            for (_, item) in items {
                collect_formula_refs(item, refs);
            }
        }
        ExprKind::Lit(_) | ExprKind::Prop(_) | ExprKind::Unsupported { .. } => {}
    }
}

fn unsupported_expr(raw: &str, reason: impl Into<String>) -> Expr {
    Expr {
        span: Span {
            start: 0,
            end: raw.len() as u32,
        },
        kind: ExprKind::Unsupported {
            raw: raw.to_string(),
            reason: reason.into(),
        },
    }
}

fn yaml_string(value: &Yaml) -> Option<&str> {
    match value {
        Yaml::String(s) => Some(s),
        _ => None,
    }
}

fn yaml_key_to_string(key: &Yaml) -> String {
    match key {
        Yaml::String(s) => s.clone(),
        other => yaml_value_to_string(other),
    }
}

fn yaml_value_to_string(value: &Yaml) -> String {
    match value {
        Yaml::String(s) => s.clone(),
        Yaml::Integer(i) => i.to_string(),
        Yaml::Real(s) => s.clone(),
        Yaml::Boolean(b) => b.to_string(),
        Yaml::Null => String::new(),
        Yaml::BadValue => "<bad>".to_string(),
        Yaml::Array(_) | Yaml::Hash(_) | Yaml::Alias(_) => format!("{value:?}"),
    }
}

fn yaml_type_name(value: &Yaml) -> &'static str {
    match value {
        Yaml::Real(_) => "real",
        Yaml::Integer(_) => "integer",
        Yaml::String(_) => "string",
        Yaml::Boolean(_) => "boolean",
        Yaml::Array(_) => "array",
        Yaml::Hash(_) => "hash",
        Yaml::Alias(_) => "alias",
        Yaml::Null => "null",
        Yaml::BadValue => "bad-value",
    }
}

fn yaml_to_json(value: &Yaml) -> JsonValue {
    match value {
        Yaml::String(s) => JsonValue::String(s.clone()),
        Yaml::Integer(i) => JsonValue::Number(JsonNumber::from(*i)),
        Yaml::Real(s) => s
            .parse::<f64>()
            .ok()
            .and_then(JsonNumber::from_f64)
            .map(JsonValue::Number)
            .unwrap_or_else(|| JsonValue::String(s.clone())),
        Yaml::Boolean(b) => JsonValue::Bool(*b),
        Yaml::Array(items) => JsonValue::Array(items.iter().map(yaml_to_json).collect()),
        Yaml::Hash(map) => {
            let mut out = JsonMap::new();
            for (key, value) in map {
                out.insert(yaml_key_to_string(key), yaml_to_json(value));
            }
            JsonValue::Object(out)
        }
        Yaml::Alias(_) | Yaml::Null | Yaml::BadValue => JsonValue::Null,
    }
}

fn is_builtin_summary(name: &str) -> bool {
    matches!(
        name.to_ascii_lowercase().as_str(),
        "count"
            | "filled"
            | "empty"
            | "unique"
            | "min"
            | "max"
            | "sum"
            | "average"
            | "earliest"
            | "latest"
            | "range"
            | "median"
            | "stddev"
            | "checked"
            | "unchecked"
    )
}

#[derive(Debug, Default)]
struct StructuralRegions {
    top_level: HashMap<String, PreservedRegion>,
    formulas: HashMap<String, PreservedRegion>,
    properties: HashMap<String, PreservedRegion>,
    summaries: HashMap<String, PreservedRegion>,
    views: Vec<ViewSpans>,
}

#[derive(Debug)]
struct SourceNode {
    start: usize,
    end: usize,
    kind: SourceNodeKind,
}

#[derive(Debug)]
enum SourceNodeKind {
    Scalar {
        value: String,
    },
    Sequence {
        flow: bool,
        items: Vec<SourceNode>,
    },
    Mapping {
        flow: bool,
        entries: Vec<SourceMappingEntry>,
    },
    Alias,
}

#[derive(Debug)]
struct SourceMappingEntry {
    key: SourceNode,
    value: SourceNode,
}

fn structural_regions(source: &str) -> Option<StructuralRegions> {
    let root = source_document(source)?;
    let top_level = mapping_regions(source, &root);
    let formulas = mapping_value(&root, "formulas")
        .map(|node| mapping_regions(source, node))
        .unwrap_or_default();
    let properties = mapping_value(&root, "properties")
        .map(|node| mapping_regions(source, node))
        .unwrap_or_default();
    let summaries = mapping_value(&root, "summaries")
        .map(|node| mapping_regions(source, node))
        .unwrap_or_default();
    let views = mapping_value(&root, "views")
        .map(|node| view_spans_from_node(source, node))
        .unwrap_or_default();
    Some(StructuralRegions {
        top_level,
        formulas,
        properties,
        summaries,
        views,
    })
}

fn source_document(source: &str) -> Option<SourceNode> {
    let mut parser = YamlParser::new_from_str(source);
    let marker_offsets = MarkerByteOffsets::new(source);
    loop {
        let (event, _) = parser.next_token().ok()?;
        match event {
            YamlEvent::DocumentStart => {
                let (event, marker) = parser.next_token().ok()?;
                return source_node(
                    &mut parser,
                    source,
                    &marker_offsets,
                    event,
                    marker_offsets.byte_offset(marker)?,
                    false,
                );
            }
            YamlEvent::StreamEnd => return None,
            _ => {}
        }
    }
}

struct MarkerByteOffsets {
    by_index: Vec<usize>,
    line_char_starts: Vec<usize>,
}

impl MarkerByteOffsets {
    fn new(source: &str) -> Self {
        let by_index = source
            .char_indices()
            .map(|(offset, _)| offset)
            .chain(std::iter::once(source.len()))
            .collect();
        let mut line_char_starts = vec![0];
        let mut chars = source.chars().peekable();
        let mut char_index = 0usize;
        while let Some(ch) = chars.next() {
            char_index += 1;
            if ch == '\r' {
                if chars.peek() == Some(&'\n') {
                    chars.next();
                    char_index += 1;
                }
                line_char_starts.push(char_index);
            } else if ch == '\n' {
                line_char_starts.push(char_index);
            }
        }
        Self {
            by_index,
            line_char_starts,
        }
    }

    fn byte_offset(&self, marker: YamlMarker) -> Option<usize> {
        let by_line_and_column = marker.line().checked_sub(1).and_then(|line| {
            let line_start = *self.line_char_starts.get(line)?;
            self.by_index.get(line_start + marker.col()).copied()
        });
        by_line_and_column.or_else(|| self.by_index.get(marker.index()).copied())
    }
}

fn source_node(
    parser: &mut YamlParser<core::str::Chars<'_>>,
    source: &str,
    marker_offsets: &MarkerByteOffsets,
    event: YamlEvent,
    start: usize,
    in_flow: bool,
) -> Option<SourceNode> {
    match event {
        YamlEvent::Scalar(value, style, _, _) => Some(SourceNode {
            start,
            end: scalar_source_end(source, start, style, in_flow),
            kind: SourceNodeKind::Scalar { value },
        }),
        YamlEvent::Alias(_) => Some(SourceNode {
            start,
            end: start,
            kind: SourceNodeKind::Alias,
        }),
        YamlEvent::SequenceStart(_, _) => {
            let flow = source.as_bytes().get(start) == Some(&b'[');
            let mut items = Vec::new();
            loop {
                let (event, marker) = parser.next_token().ok()?;
                if event == YamlEvent::SequenceEnd {
                    return Some(SourceNode {
                        start,
                        end: collection_end(
                            source,
                            marker_offsets.byte_offset(marker)?,
                            flow,
                            b']',
                        ),
                        kind: SourceNodeKind::Sequence { flow, items },
                    });
                }
                items.push(source_node(
                    parser,
                    source,
                    marker_offsets,
                    event,
                    marker_offsets.byte_offset(marker)?,
                    flow,
                )?);
            }
        }
        YamlEvent::MappingStart(_, _) => {
            let flow = source.as_bytes().get(start) == Some(&b'{');
            let mut entries = Vec::new();
            loop {
                let (event, marker) = parser.next_token().ok()?;
                if event == YamlEvent::MappingEnd {
                    return Some(SourceNode {
                        start,
                        end: collection_end(
                            source,
                            marker_offsets.byte_offset(marker)?,
                            flow,
                            b'}',
                        ),
                        kind: SourceNodeKind::Mapping { flow, entries },
                    });
                }
                let key = source_node(
                    parser,
                    source,
                    marker_offsets,
                    event,
                    marker_offsets.byte_offset(marker)?,
                    flow,
                )?;
                let (event, marker) = parser.next_token().ok()?;
                let value = source_node(
                    parser,
                    source,
                    marker_offsets,
                    event,
                    marker_offsets.byte_offset(marker)?,
                    flow,
                )?;
                entries.push(SourceMappingEntry { key, value });
            }
        }
        _ => None,
    }
}

fn collection_end(source: &str, marker: usize, flow: bool, closing: u8) -> usize {
    if !flow {
        return marker;
    }
    let bytes = source.as_bytes();
    let mut cursor = marker;
    while let Some(&byte) = bytes.get(cursor) {
        if byte == closing {
            return cursor + 1;
        }
        if byte == b',' || byte.is_ascii_whitespace() {
            cursor += 1;
            continue;
        }
        if byte == b'#' {
            cursor += 1;
            while bytes
                .get(cursor)
                .is_some_and(|byte| !matches!(byte, b'\r' | b'\n'))
            {
                cursor += 1;
            }
            continue;
        }
        break;
    }
    marker
}

fn scalar_source_end(source: &str, start: usize, style: TScalarStyle, in_flow: bool) -> usize {
    let bytes = source.as_bytes();
    match style {
        TScalarStyle::SingleQuoted => quoted_scalar_end(bytes, start, b'\'', true),
        TScalarStyle::DoubleQuoted => quoted_scalar_end(bytes, start, b'"', false),
        TScalarStyle::Plain => {
            let mut end = start;
            while end < bytes.len() {
                let byte = bytes[end];
                if matches!(byte, b',' | b']' | b'}') || (!in_flow && matches!(byte, b'\r' | b'\n'))
                {
                    break;
                }
                if byte == b'#' && (end == start || bytes[end - 1].is_ascii_whitespace()) {
                    break;
                }
                if byte == b':'
                    && bytes.get(end + 1).is_none_or(|next| {
                        next.is_ascii_whitespace() || matches!(next, b',' | b']' | b'}')
                    })
                {
                    break;
                }
                end += 1;
            }
            while end > start && bytes[end - 1].is_ascii_whitespace() {
                end -= 1;
            }
            end
        }
        TScalarStyle::Literal | TScalarStyle::Folded => source[start..]
            .find('\n')
            .map_or(source.len(), |offset| start + offset),
    }
}

fn quoted_scalar_end(bytes: &[u8], start: usize, quote: u8, doubled_quote: bool) -> usize {
    let mut cursor = start.saturating_add(1);
    let mut escaped = false;
    while cursor < bytes.len() {
        let byte = bytes[cursor];
        if !doubled_quote && escaped {
            escaped = false;
            cursor += 1;
            continue;
        }
        if !doubled_quote && byte == b'\\' {
            escaped = true;
            cursor += 1;
            continue;
        }
        if byte == quote {
            if doubled_quote && bytes.get(cursor + 1) == Some(&quote) {
                cursor += 2;
                continue;
            }
            return cursor + 1;
        }
        cursor += 1;
    }
    bytes.len()
}

fn mapping_value<'a>(node: &'a SourceNode, name: &str) -> Option<&'a SourceNode> {
    let SourceNodeKind::Mapping { entries, .. } = &node.kind else {
        return None;
    };
    entries.iter().find_map(|entry| {
        let SourceNodeKind::Scalar { value } = &entry.key.kind else {
            return None;
        };
        (value == name).then_some(&entry.value)
    })
}

fn mapping_regions(source: &str, node: &SourceNode) -> HashMap<String, PreservedRegion> {
    let SourceNodeKind::Mapping { flow, entries } = &node.kind else {
        return HashMap::new();
    };
    let mut regions = HashMap::new();
    for (index, entry) in entries.iter().enumerate() {
        let SourceNodeKind::Scalar { value: name } = &entry.key.kind else {
            continue;
        };
        let start = if *flow {
            entry.key.start
        } else {
            line_start(source, entry.key.start)
        };
        let end = if *flow {
            entry.value.end
        } else {
            entries
                .get(index + 1)
                .map(|next| line_start(source, next.key.start))
                .unwrap_or(node.end)
        };
        let end = if *flow {
            end
        } else {
            trim_trailing_interstitial_lines(source, start, end)
        };
        regions.insert(name.clone(), preserved_region(source, start, end));
    }
    regions
}

fn view_spans_from_node(source: &str, node: &SourceNode) -> Vec<ViewSpans> {
    let SourceNodeKind::Sequence { flow, items } = &node.kind else {
        return Vec::new();
    };
    items
        .iter()
        .enumerate()
        .map(|(index, item)| {
            let start = if *flow {
                item.start
            } else {
                line_start(source, node_content_start(item))
            };
            let end = if *flow {
                item.end
            } else {
                items
                    .get(index + 1)
                    .map(|next| line_start(source, node_content_start(next)))
                    .unwrap_or(node.end)
            };
            let entry = preserved_region(source, start, end);
            let key_regions = mapping_regions(source, item);
            let filters = key_regions
                .get("filters")
                .map(|region| filter_node_regions_in_span(source, region.span))
                .unwrap_or_default();
            ViewSpans {
                entry,
                keys: named_regions_from_map(&key_regions),
                filters,
            }
        })
        .collect()
}

fn node_content_start(node: &SourceNode) -> usize {
    match &node.kind {
        SourceNodeKind::Mapping { entries, .. } => {
            entries.first().map_or(node.start, |entry| entry.key.start)
        }
        _ => node.start,
    }
}

fn line_start(source: &str, offset: usize) -> usize {
    source[..offset.min(source.len())]
        .rfind('\n')
        .map_or(0, |index| index + 1)
}

fn named_regions_from_map(regions: &HashMap<String, PreservedRegion>) -> Vec<NamedRegion> {
    let mut out: Vec<NamedRegion> = regions
        .iter()
        .map(|(name, region)| NamedRegion {
            name: name.clone(),
            region: region.clone(),
        })
        .collect();
    out.sort_by_key(|named| named.region.span.start);
    out
}

fn named_region_map(regions: &[NamedRegion]) -> HashMap<String, PreservedRegion> {
    regions
        .iter()
        .map(|named| (named.name.clone(), named.region.clone()))
        .collect()
}

fn filter_node_regions_in_span(source: &str, span: Span) -> Vec<PreservedRegion> {
    let mut starts = vec![(span.start as usize, 0usize)];
    for line in line_infos(source) {
        if line.start <= span.start as usize || line.start >= span.end as usize {
            continue;
        }
        let trimmed = line.text.trim_start();
        if trimmed.trim().is_empty() || trimmed.starts_with('#') {
            continue;
        }
        let indent = line.text.len() - trimmed.len();
        if trimmed.starts_with("- ") {
            starts.push((line.start, indent));
        }
    }

    let mut regions = Vec::new();
    for (idx, (start, indent)) in starts.iter().enumerate() {
        let end = starts
            .iter()
            .skip(idx + 1)
            .find_map(|(next_start, next_indent)| (*next_indent <= *indent).then_some(*next_start))
            .unwrap_or(span.end as usize);
        regions.push(preserved_region(source, *start, end));
    }
    regions
}

fn regions_in_span(source: &str, span: Span, indent: usize) -> HashMap<String, PreservedRegion> {
    let lines = line_infos(source);
    let mut starts = Vec::new();
    for line in &lines {
        if line.start < span.start as usize || line.start >= span.end as usize {
            continue;
        }
        if let Some((key, actual_indent)) = key_on_line(line.text)
            && actual_indent == indent
        {
            starts.push((key, line.start));
        }
    }
    let mut regions = HashMap::new();
    for (idx, (key, start)) in starts.iter().enumerate() {
        let end = starts
            .get(idx + 1)
            .map(|(_, next_start)| *next_start)
            .unwrap_or(span.end as usize);
        let end = trim_trailing_interstitial_lines(source, *start, end);
        regions.insert(key.clone(), preserved_region(source, *start, end));
    }
    regions
}

fn child_mapping_indent(region: &PreservedRegion) -> Option<usize> {
    let mut indents = region
        .text
        .lines()
        .filter_map(|line| key_on_line(line).map(|(_, indent)| indent));
    let parent_indent = indents.next()?;
    indents.filter(|indent| *indent > parent_indent).min()
}

fn trim_trailing_interstitial_lines(source: &str, start: usize, end: usize) -> usize {
    let mut trimmed_end = end;
    for line in line_infos(source).into_iter().rev() {
        if line.start < start || line.start >= trimmed_end {
            continue;
        }
        if line.start == start {
            break;
        }

        let line_end = (line.start + line.text.len()).min(trimmed_end);
        let text = &source[line.start..line_end];
        let trimmed = text.trim();
        if trimmed.is_empty() || trimmed.starts_with('#') {
            trimmed_end = line.start;
        } else {
            break;
        }
    }
    trimmed_end
}

fn key_on_line(line: &str) -> Option<(String, usize)> {
    let trimmed = line.trim_start();
    if trimmed.is_empty() || trimmed.starts_with('#') {
        return None;
    }
    let indent = line.len() - trimmed.len();
    let candidate = trimmed.strip_prefix("- ").unwrap_or(trimmed);
    let colon = key_colon_index(candidate)?;
    let raw_key = candidate[..colon].trim();
    if raw_key.is_empty() || raw_key.starts_with('#') {
        return None;
    }
    let key = if (raw_key.starts_with('"') && raw_key.ends_with('"'))
        || (raw_key.starts_with('\'') && raw_key.ends_with('\''))
    {
        raw_key[1..raw_key.len() - 1].to_string()
    } else {
        if raw_key.contains(' ') {
            return None;
        }
        raw_key.to_string()
    };
    Some((key, indent + usize::from(trimmed.starts_with("- ")) * 2))
}

fn key_colon_index(candidate: &str) -> Option<usize> {
    let mut quote = None;
    let mut escaped = false;
    for (idx, ch) in candidate.char_indices() {
        match quote {
            Some('"') if escaped => {
                escaped = false;
            }
            Some('"') if ch == '\\' => {
                escaped = true;
            }
            Some(active) if ch == active => {
                quote = None;
            }
            Some(_) => {}
            None if ch == '"' || ch == '\'' => {
                quote = Some(ch);
            }
            None if ch == ':' => return Some(idx),
            None => {}
        }
    }
    None
}

fn preserved_region(source: &str, start: usize, end: usize) -> PreservedRegion {
    PreservedRegion {
        span: Span {
            start: start as u32,
            end: end as u32,
        },
        text: source[start..end].to_string(),
    }
}

#[derive(Debug)]
struct LineInfo<'a> {
    start: usize,
    text: &'a str,
}

fn line_infos(source: &str) -> Vec<LineInfo<'_>> {
    let mut lines = Vec::new();
    let mut start = 0usize;
    for line in source.split_inclusive('\n') {
        lines.push(LineInfo { start, text: line });
        start += line.len();
    }
    if start < source.len() {
        lines.push(LineInfo {
            start,
            text: &source[start..],
        });
    }
    lines
}
