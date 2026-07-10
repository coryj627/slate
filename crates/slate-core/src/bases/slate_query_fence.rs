// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Classification for dual-mode `slate-query` fenced blocks.
//!
//! A top-level `query` scalar selects saved-query reference mode. Every other
//! valid YAML root selects inline Bases mode. Keeping this decision in Core
//! prevents host UIs from growing incomplete line-oriented YAML parsers.

use thiserror::Error;
use yaml_rust2::{Yaml, YamlLoader};

/// The decoded routing fields for a `slate-query` fence.
///
/// `query == None` means the body is inline Bases YAML. `view` is populated
/// only for saved-query reference mode.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct SlateQueryFenceClassification {
    pub query: Option<String>,
    pub view: Option<String>,
}

/// A classification error that must be surfaced instead of reinterpreting an
/// invalid saved-query reference as inline Bases YAML.
#[derive(Debug, Clone, PartialEq, Eq, Error)]
pub enum SlateQueryFenceError {
    #[error("invalid slate-query YAML: {message}")]
    InvalidYaml { message: String },

    #[error("slate-query body must contain one YAML document; got {count}")]
    MultipleDocuments { count: usize },

    #[error("slate-query query must be a scalar; got {actual}")]
    NonScalarQuery { actual: String },

    #[error("slate-query query must be a string, number, or boolean; got {actual}")]
    InvalidQueryScalar { actual: String },

    #[error("slate-query query must not be empty")]
    EmptyQuery,

    #[error("slate-query view must be a scalar; got {actual}")]
    NonScalarView { actual: String },

    #[error("slate-query view must be a string, number, or boolean; got {actual}")]
    InvalidViewScalar { actual: String },
}

/// Classify a `slate-query` fence body as an inline Base or a saved-query
/// reference, using full YAML scalar semantics for the reference fields.
pub fn classify_slate_query_fence(
    source: &str,
) -> Result<SlateQueryFenceClassification, SlateQueryFenceError> {
    let documents =
        YamlLoader::load_from_str(source).map_err(|error| SlateQueryFenceError::InvalidYaml {
            message: error.to_string(),
        })?;

    if documents.len() > 1 {
        return Err(SlateQueryFenceError::MultipleDocuments {
            count: documents.len(),
        });
    }

    let Some(Yaml::Hash(root)) = documents.first() else {
        return Ok(inline_classification());
    };
    let query_key = Yaml::String("query".to_string());
    let Some(query) = root.get(&query_key) else {
        return Ok(inline_classification());
    };

    let query = reference_scalar(query).map_err(|kind| match kind {
        ScalarError::NonScalar(actual) => SlateQueryFenceError::NonScalarQuery {
            actual: actual.to_string(),
        },
        ScalarError::Invalid(actual) => SlateQueryFenceError::InvalidQueryScalar {
            actual: actual.to_string(),
        },
    })?;
    if query.trim().is_empty() {
        return Err(SlateQueryFenceError::EmptyQuery);
    }

    let view_key = Yaml::String("view".to_string());
    let view = root
        .get(&view_key)
        .map(reference_scalar)
        .transpose()
        .map_err(|kind| match kind {
            ScalarError::NonScalar(actual) => SlateQueryFenceError::NonScalarView {
                actual: actual.to_string(),
            },
            ScalarError::Invalid(actual) => SlateQueryFenceError::InvalidViewScalar {
                actual: actual.to_string(),
            },
        })?;

    Ok(SlateQueryFenceClassification {
        query: Some(query),
        view,
    })
}

fn inline_classification() -> SlateQueryFenceClassification {
    SlateQueryFenceClassification {
        query: None,
        view: None,
    }
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
enum ScalarError {
    NonScalar(&'static str),
    Invalid(&'static str),
}

fn reference_scalar(value: &Yaml) -> Result<String, ScalarError> {
    match value {
        Yaml::String(value) => Ok(value.clone()),
        Yaml::Integer(value) => Ok(value.to_string()),
        Yaml::Real(value) => Ok(value.clone()),
        Yaml::Boolean(value) => Ok(value.to_string()),
        Yaml::Array(_) => Err(ScalarError::NonScalar("array")),
        Yaml::Hash(_) => Err(ScalarError::NonScalar("hash")),
        Yaml::Null => Err(ScalarError::Invalid("null")),
        Yaml::Alias(_) => Err(ScalarError::Invalid("alias")),
        Yaml::BadValue => Err(ScalarError::Invalid("bad-value")),
    }
}
