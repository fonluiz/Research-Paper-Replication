'''Hyperparameter Optimization
'''

import numpy
import pandas as pd

from sklearn.ensemble import RandomForestClassifier
from sklearn.model_selection import RandomizedSearchCV
from scipy.stats import randint as sp_randint
import sys

# Specification of event log
log = sys.argv[1]
timestepsFrom = int(sys.argv[2])
timestepsTo = int(sys.argv[3])
num_classes = 2
batch_size = 128

for i in range(2, 6, 3):  # Use range(2, 6, 3) for rtfm
    num_timesteps = i
    # import dataset
    dataset = numpy.loadtxt("../data/transformed/" + log + "/" + log + "_transformed_" + str(num_timesteps) + ".csv", delimiter=";")

    # Sampling a too big dataset
    # dataset = pd.read_csv("../data/transformed/" + log + "/" + log + "_transformed_" + str(num_timesteps) + ".csv",
    #                         delimiter=";", header=None)
    # dataset = dataset.groupby(len(dataset.columns)-1, group_keys=False).apply(lambda x: x.sample(frac=0.05)).reset_index().to_numpy()

    X_train = dataset[:, :-1]
    Y_train = dataset[:, -1]
    if sys.argv[1] != "ProductionLog":
        Y_train[Y_train == 1] = 0
        Y_train[Y_train == 2] = 1
        Y_train[Y_train == 3] = 1

    num_features = int((dataset.shape[1] - 1) / num_timesteps)

    if __name__ == '__main__':
        y_train = Y_train
        scoring = {'AUC': 'roc_auc',
                   'Accuracy': 'accuracy',
                   'F1': 'f1',
                   'Precision': 'precision',
                   'Recall': 'recall'}

        # Fixed random seed for reproducibility
        seed = 7
        numpy.random.seed(seed)

        # define grid search parameters
        param_dist = {'n_estimators': [50, 100, 200, 400, 800, 1600],
                      "max_depth": [3, None],
                      "max_features": sp_randint(1, num_features),
                      "min_samples_split": sp_randint(2, 11),
                      "min_samples_leaf": sp_randint(1, 11),
                      "bootstrap": [True, False],
                      "criterion": ["gini", "entropy"]}
        # create model

        clf = RandomForestClassifier()

        randomizedSearch = RandomizedSearchCV(estimator=clf, param_distributions=param_dist, scoring=scoring, cv=3,
                                              n_jobs=-1, refit='F1', n_iter=20)
        grid_search_result = randomizedSearch.fit(X_train, y_train)

        # output
        print("Best: %f using %s" % (grid_search_result.best_score_, grid_search_result.best_params_))

        metrics = numpy.empty(5)
        metrics[0] = grid_search_result.cv_results_['mean_test_AUC'][grid_search_result.best_index_]
        metrics[1] = grid_search_result.cv_results_['mean_test_Accuracy'][grid_search_result.best_index_]
        metrics[2] = grid_search_result.cv_results_['mean_test_F1'][grid_search_result.best_index_]
        metrics[3] = grid_search_result.cv_results_['mean_test_Precision'][grid_search_result.best_index_]
        metrics[4] = grid_search_result.cv_results_['mean_test_Recall'][grid_search_result.best_index_]

        numpy.savetxt("../data/results/rf/"+log+"/"+log+"_" + str(num_timesteps) + ".csv", numpy.atleast_2d(metrics),
                      delimiter=',', fmt='%6f', header="AUC, Accuracy, F1, Precision, Recall")
        text_file = open("../data/results/rf/"+log+"/hyperparameters"+log+"_" + str(num_timesteps) + ".txt", "w")
        text_file.write(str(grid_search_result.best_params_))
        text_file.close()
