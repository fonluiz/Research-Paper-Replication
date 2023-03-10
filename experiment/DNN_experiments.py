import sys

import numpy
from hyperas import optim
from hyperas.distributions import choice, uniform
from hyperopt import Trials, STATUS_OK, tpe
from keras.layers import Dense, LSTM, SimpleRNN
from keras.models import Sequential
from keras.utils import to_categorical
import keras.optimizers
from sklearn.metrics import accuracy_score
from sklearn.metrics import f1_score
from sklearn.metrics import precision_score
from sklearn.metrics import recall_score
from sklearn.metrics import roc_auc_score
from sklearn.model_selection import train_test_split
from sklearn.preprocessing import normalize
from sklearn.model_selection import StratifiedKFold
import globalvar
import pandas as pd
import tensorflow as tf
import numpy as np


log = sys.argv[1]
timestepsFrom = int(sys.argv[2])
timestepsTo = int(sys.argv[3])


def data():
    dataset = numpy.loadtxt(
        "../data/transformed/" + sys.argv[1] + "/" + sys.argv[1] + "_transformed_" + sys.argv[2] + ".csv",
        delimiter=";")
    # dataset = pd.read_csv("../data/transformed/" + sys.argv[1] + "/" + sys.argv[1] + "_transformed_" + sys.argv[2] + ".csv",
    #                         delimiter=";")
    # dataset = dataset.groupby(dataset.columns[len(dataset.columns) - 1], group_keys=False).apply(
    #     lambda x: x.sample(frac=0.05)).reset_index().to_numpy()

    X = dataset[:, :-1]
    Y = dataset[:, -1]
    X = normalize(X)
    if sys.argv[1] != "ProductionLog":
        Y[Y == 1] = 0
        Y[Y == 2] = 1
        Y[Y == 3] = 1

    X_train, X_test, Y_train, Y_test = train_test_split(X, Y, stratify=Y, test_size=0.25)
    print(X_train)

    # X_train = X_train.reshape((X_train.shape[0], int(sys.argv[2]), int(sys.argv[3])))

    # X_test = X_test.reshape((X_test.shape[0], int(sys.argv[2]), int(sys.argv[3])))

    y_train = to_categorical(Y_train, 2)
    y_test = to_categorical(Y_test, 2)

    return X_train, y_train, X_test, y_test, X, Y


def create_model(X_train, y_train, X_test, y_test):
    model = Sequential()
    model.add(tf.keras.layers.Dense(256, input_shape=(X_train.shape[1],), activation='sigmoid'))
    model.add(Dense(64, input_shape=(dataset.shape[1] - 1,)))
    if ({{choice(['two', 'three'])}}) == 'three':
        model.add(Dense({{choice([64, 128, 256, 512, 1024])}}))
        if ({{choice(['three', 'four'])}}) == 'four':
            model.add(Dense({{choice([64, 128, 256, 512, 1024])}}))
    model.add(Dense({{choice([64, 128, 256, 512, 1024])}}))
    model.add(Dense(2, activation='sigmoid'))

    model.summary()

    adam = keras.optimizers.Adam(lr={{choice([10 ** -6, 10 ** -5, 10 ** -4, 10 ** -3, 10 ** -2, 10 ** -1])}},
                                 clipnorm=1.)
    rmsprop = keras.optimizers.RMSprop(lr={{choice([10 ** -6, 10 ** -5, 10 ** -4, 10 ** -3, 10 ** -2, 10 ** -1])}},
                                       clipnorm=1.)
    sgd = keras.optimizers.SGD(lr={{choice([10 ** -6, 10 ** -5, 10 ** -4, 10 ** -3, 10 ** -2, 10 ** -1])}}, clipnorm=1.)

    choiceval = {{choice(['adam', 'sgd', 'rmsprop'])}}
    if choiceval == 'adam':
        optim = adam
    elif choiceval == 'rmsprop':
        optim = rmsprop
    else:
        optim = sgd

    model.compile(loss='binary_crossentropy',
                  optimizer=optim,
                  metrics=['accuracy', globalvar.f1, globalvar.precision, globalvar.recall, globalvar.auc])
    callbacks_list = [globalvar.earlystop]
    model.fit(X_train, y_train,
              batch_size={{choice([32, 64, 128])}},
              epochs={{choice([50])}},
              callbacks=callbacks_list,
              validation_data=(X_test, y_test),
              verbose=0
              )
    score = model.evaluate(X_test, y_test, verbose=0)

    accuracy = score[1]
    return {'loss': -accuracy, 'status': STATUS_OK, 'model': model}


if __name__ == "__main__":
    dataset = numpy.loadtxt(
        "../data/transformed/" + sys.argv[1] + "/" + sys.argv[1] + "_transformed_" + sys.argv[2] + ".csv",
        delimiter=";")
    # dataset = pd.read_csv("../data/transformed/" + sys.argv[1] + "/" + sys.argv[1] + "_transformed_" + sys.argv[2] + ".csv",
    #     delimiter=";")
    # dataset = dataset.groupby(dataset.columns[len(dataset.columns) - 1], group_keys=False).apply(
    #     lambda x: x.sample(frac=0.05)).reset_index().to_numpy()
    sys.argv[3] = str(int((dataset.shape[1] - 1) / int(sys.argv[2])))
    globalvar.timestep = int(sys.argv[2])
    globalvar.numfeatures = int((dataset.shape[1] - 1) / globalvar.timestep)

    print(dataset.shape[1] - 1)

    best_run, best_model = optim.minimize(model=create_model,
                                          data=data,
                                          algo=tpe.suggest,
                                          max_evals=3,
                                          trials=Trials(),
                                          eval_space=True,
                                          )

    X_train, Y_train, X_test, Y_test, X, Y = data()

    print("Evalutation of best performing model:")
    print(best_model.evaluate(X_test, Y_test))
    print(best_model.metrics_names)

    print("Best performing model chosen hyper-parameters:")
    print(best_run)

    kfold = StratifiedKFold(n_splits=3, shuffle=True, random_state=7)
    cvscoresAcc = []
    cvscoresF1 = []
    cvscoresPrecision = []
    cvscoresRecall = []
    cvscoresAUC = []
    cvscoresAccSK = []
    cvscoresF1SK = []
    cvscoresPrecisionSK = []
    cvscoresRecallSK = []
    cvscoresAUCSK = []

    # X = X.reshape((X.shape[0], globalvar.timestep, globalvar.numfeatures))
    Y_cat = to_categorical(Y, 2)
    callbacks_list = [globalvar.earlystop]

    for train, test in kfold.split(X, Y):
        best_model.fit(X[train], Y_cat[train], epochs=best_run["epochs"], callbacks=callbacks_list,
                       batch_size=best_run["batch_size"],
                       verbose=0)

        y_pred = np.argmax(best_model.predict(X_test), axis=1).round()
        print(y_pred)
        scores = best_model.evaluate(X[test], Y_cat[test], verbose=0)
        print("%s: %.2f%%" % (best_model.metrics_names[1], scores[1] * 100))
        cvscoresAcc.append(scores[1])
        cvscoresF1.append(scores[2])
        cvscoresPrecision.append(scores[3])
        cvscoresRecall.append(scores[4])
        cvscoresAUC.append(scores[5])
        y_pred_sparse = y_pred
        y_test_sparse = [numpy.argmax(y, axis=None, out=None) for y in Y_test]

        cvscoresF1SK.append(f1_score(y_test_sparse, y_pred_sparse))
        cvscoresAccSK.append(accuracy_score(y_test_sparse, y_pred_sparse))
        cvscoresRecallSK.append(recall_score(y_test_sparse, y_pred_sparse))
        cvscoresPrecisionSK.append(precision_score(y_test_sparse, y_pred_sparse))
        cvscoresAUCSK.append(roc_auc_score(y_test_sparse, y_pred_sparse))

    print("%.2f%% (+/- %.2f%%)" % (numpy.mean(cvscoresAcc), numpy.std(cvscoresAcc)))
    measures = [numpy.mean(cvscoresAcc), numpy.mean(cvscoresF1), numpy.mean(cvscoresPrecision),
                numpy.mean(cvscoresRecall), numpy.mean(cvscoresAUC)]
    measuresSK = [numpy.mean(cvscoresAccSK), numpy.mean(cvscoresF1SK), numpy.mean(cvscoresPrecisionSK),
                  numpy.mean(cvscoresRecallSK), numpy.mean(cvscoresAUCSK)]
    numpy.savetxt("../data/results/dnn/" + sys.argv[1] + "/" + sys.argv[1] + "_" + str(sys.argv[2]) + ".csv",
                  numpy.atleast_2d(measures), delimiter=',', fmt='%6f', header="acc, f1, precision, recall, auc")
    numpy.savetxt("../data/results/dnn/" + sys.argv[1] + "/" + sys.argv[1] + "_" + str(sys.argv[2]) + "_SK.csv",
                  numpy.atleast_2d(measuresSK), delimiter=',', fmt='%6f', header="acc, f1, precision, recall, auc")
    text_file = open("../data/results/dnn/" + sys.argv[1] + "/hyperparameters" + sys.argv[1] + "_" + str(sys.argv[2]) + ".txt", "w")
    text_file.write(str(best_run))
    text_file.close()
